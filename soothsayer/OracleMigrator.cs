﻿using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using soothsayer.Infrastructure;
using soothsayer.Infrastructure.IO;
using soothsayer.Migrations;
using soothsayer.Scanners;
using soothsayer.Scripts;

namespace soothsayer
{
    public class OracleMigrator : IMigrator
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly IVersionRespositoryFactory _versionRespositoryFactory;
        private readonly IAppliedScriptsRepositoryFactory _appliedScriptsRepositoryFactory;
        private readonly IDatabaseMetadataProviderFactory _databaseMetadataProviderFactory;
        private readonly IScriptScannerFactory _scriptScannerFactory;
        private readonly IScriptRunnerFactory _scriptRunnerFactory;

        public OracleMigrator(
            IConnectionFactory connectionFactory,
            IVersionRespositoryFactory versionRespositoryFactory,
            IAppliedScriptsRepositoryFactory appliedScriptsRepositoryFactory,
            IDatabaseMetadataProviderFactory databaseMetadataProviderFactory,
            IScriptScannerFactory scriptScannerFactory,
            IScriptRunnerFactory scriptRunnerFactory)
        {
            _connectionFactory = connectionFactory;
            _versionRespositoryFactory = versionRespositoryFactory;
            _databaseMetadataProviderFactory = databaseMetadataProviderFactory;
            _scriptScannerFactory = scriptScannerFactory;
            _scriptRunnerFactory = scriptRunnerFactory;
            _appliedScriptsRepositoryFactory = appliedScriptsRepositoryFactory;
        }

        public void Migrate(DatabaseConnectionInfo databaseConnectionInfo, MigrationInfo migrationInfo)
        {
            using (var connection = _connectionFactory.Create(databaseConnectionInfo))
            {
                Output.Text("Connected to oracle database on connection string '{0}'.".FormatWith(databaseConnectionInfo.ConnectionString));
                Output.EmptyLine();

                Output.Text("Checking for the current database version.");
                var oracleMetadataProvider = _databaseMetadataProviderFactory.Create(connection);
                var oracleVersioning = _versionRespositoryFactory.Create(connection);
                var oracleAppliedScriptsRepository = _appliedScriptsRepositoryFactory.Create(connection);

                var currentVersion = oracleMetadataProvider.SchemaExists(migrationInfo.TargetSchema) ? oracleVersioning.GetCurrentVersion(migrationInfo.TargetSchema) : null;
                Output.Info("The current database version is: {0}".FormatWith(currentVersion.IsNotNull() ? currentVersion.Version.ToString(CultureInfo.InvariantCulture) : "<empty>"));
                Output.EmptyLine();

                Output.Info("Scanning input folder '{0}' for scripts...".FormatWith(migrationInfo.ScriptFolder));

                var initScripts = ScanForScripts(migrationInfo, ScriptFolders.Init, _scriptScannerFactory.Create(ScriptFolders.Init)).ToArray();
                var upScripts = ScanForScripts(migrationInfo, ScriptFolders.Up, _scriptScannerFactory.Create(ScriptFolders.Up)).ToArray();
                var downScripts = ScanForScripts(migrationInfo, ScriptFolders.Down, _scriptScannerFactory.Create(ScriptFolders.Down)).ToArray();
                var termScripts = ScanForScripts(migrationInfo, ScriptFolders.Term, _scriptScannerFactory.Create(ScriptFolders.Term)).ToArray();
                Output.EmptyLine();

                if (migrationInfo.TargetVersion.HasValue)
                {
                    Output.Info("Target database version was provided, will target migrating the database to version {0}".FormatWith(migrationInfo.TargetVersion.Value));
                }

                VerifyDownScripts(upScripts, downScripts);

                var storedMigrationSteps = new List<IStep>();

                if (migrationInfo.UseStored)
                {
                    Output.Info("--usestored was specified, fetching set of applied scripts stored in the target database...".FormatWith());

                    storedMigrationSteps = oracleAppliedScriptsRepository.GetAppliedScripts(migrationInfo.TargetSchema).ToList();

                    Output.Text("    {0} stored applied scripts found.".FormatWith(storedMigrationSteps.Count));
                    Output.EmptyLine();
                }

                var scriptRunner = _scriptRunnerFactory.Create(databaseConnectionInfo);
                RunMigration(migrationInfo, currentVersion, initScripts, upScripts, downScripts, termScripts, storedMigrationSteps, scriptRunner, oracleMetadataProvider, oracleVersioning, oracleAppliedScriptsRepository);

                if (oracleMetadataProvider.SchemaExists(migrationInfo.TargetSchema))
                {
                    var newVersion = oracleVersioning.GetCurrentVersion(migrationInfo.TargetSchema);
                    Output.Info("Database version is now: {0}".FormatWith(newVersion.IsNotNull() ? newVersion.Version.ToString(CultureInfo.InvariantCulture) : "<empty>"));
                }
                else
                {
                    Output.Info("Target schema '{0}' no longer exists.".FormatWith(migrationInfo.TargetSchema));
                }
            }
        }

        private static IEnumerable<Script> ScanForScripts(MigrationInfo migrationInfo, string migrationFolder, IScriptScanner scanner)
        {
            var environments = (migrationInfo.TargetEnvironment ?? Enumerable.Empty<string>()).ToArray();
            var scripts = (scanner.Scan(migrationInfo.ScriptFolder.Whack(migrationFolder), environments) ?? Enumerable.Empty<Script>()).ToArray();

            Output.Text("Found {0} '{1}' scripts.".FormatWith(scripts.Length, migrationFolder));

            foreach (var script in scripts)
            {
                Output.Verbose(script.Name, 1);
            }

            return scripts;
        }

        private static void VerifyDownScripts(IEnumerable<Script> upScripts, IEnumerable<Script> downScripts)
        {
            var withoutRollback = upScripts.Where(u => downScripts.All(d => d.Version != u.Version)).ToArray();

            if (withoutRollback.Any())
            {
                Output.Warn("The following 'up' scripts do not have a corresponding 'down' script, any rollback may not work as expected:");

                foreach (var script in withoutRollback)
                {
                    Output.Warn(script.Name, 1);
                }

                Output.EmptyLine();
            }
        }

        private static void RunMigration(MigrationInfo migrationInfo, DatabaseVersion currentVersion, IEnumerable<Script> initScripts, IEnumerable<Script> upScripts, IEnumerable<Script> downScripts, IEnumerable<Script> termScripts,
            IList<IStep> storedSteps, IScriptRunner scriptRunner, IDatabaseMetadataProvider databaseMetadataProvider, IVersionRespository versionRespository, IAppliedScriptsRepository appliedScriptsRepository)
        {
            var upDownSteps = upScripts.Select(u => new DatabaseStep(u, downScripts.FirstOrDefault(d => d.Version == u.Version))).ToList();
            var initTermSteps = initScripts.Select(i => new DatabaseStep(i, termScripts.FirstOrDefault(t => t.Version == i.Version))).ToList();

            if (migrationInfo.Direction == MigrationDirection.Down)
            {
                var downMigration = new DownMigration(databaseMetadataProvider, versionRespository, appliedScriptsRepository, migrationInfo.Forced);

                if (storedSteps.Any())
                {
                    Output.Warn("NOTE: Using stored applied scripts to perform downgrade instead of local 'down' scripts.");
                    downMigration.Migrate(storedSteps, currentVersion, migrationInfo.TargetVersion, scriptRunner, migrationInfo.TargetSchema, migrationInfo.TargetTablespace);
                }
                else
                {
                    downMigration.Migrate(upDownSteps, currentVersion, migrationInfo.TargetVersion, scriptRunner, migrationInfo.TargetSchema, migrationInfo.TargetTablespace);
                }

                if (!migrationInfo.TargetVersion.HasValue)
                {
                    var termMigration = new TermMigration(databaseMetadataProvider, migrationInfo.Forced);
                    termMigration.Migrate(initTermSteps, currentVersion, migrationInfo.TargetVersion, scriptRunner, migrationInfo.TargetSchema, migrationInfo.TargetTablespace);
                }
                else
                {
                    Output.Info("A target version was provided, termination scripts will not be executed.");
                }
            }
            else
            {
                var initMigration = new InitMigration(databaseMetadataProvider, migrationInfo.Forced);
                initMigration.Migrate(initTermSteps, currentVersion, migrationInfo.TargetVersion, scriptRunner, migrationInfo.TargetSchema, migrationInfo.TargetTablespace);

                EnsureVersioningTableIsInitialised(versionRespository, migrationInfo.TargetSchema, migrationInfo.TargetTablespace);
                EnsureAppliedScriptsTableIsInitialised(appliedScriptsRepository, migrationInfo.TargetSchema, migrationInfo.TargetTablespace);

                var upMigration = new UpMigration(versionRespository, appliedScriptsRepository, migrationInfo.Forced);
                upMigration.Migrate(upDownSteps, currentVersion, migrationInfo.TargetVersion, scriptRunner, migrationInfo.TargetSchema, migrationInfo.TargetTablespace);
            }
        }

        private static void EnsureAppliedScriptsTableIsInitialised(IAppliedScriptsRepository appliedScriptsRepository, string targetSchema, string targetTablespace)
        {
            bool alreadyInitialised = appliedScriptsRepository.AppliedScriptsTableExists(targetSchema);

            if (!alreadyInitialised)
            {
                appliedScriptsRepository.InitialiseAppliedScriptsTable(targetSchema, targetTablespace);
            }
        }

        private static void EnsureVersioningTableIsInitialised(IVersionRespository versionRespository, string targetSchema, string targetTablespace)
        {
            bool alreadyInitialised = versionRespository.VersionTableExists(targetSchema);

            if (!alreadyInitialised)
            {
                versionRespository.InitialiseVersioningTable(targetSchema, targetTablespace);
            }
        }
    }
}
