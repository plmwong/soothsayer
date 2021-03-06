﻿using System.Collections.Generic;

namespace soothsayer
{
    public class MigrationInfo
    {
        public MigrationInfo(MigrationDirection direction, string scriptFolder, string targetSchema, string targetTablespace, IList<string> targetEnvironment,
            long? targetVersion, bool useStored = false, bool forced = false)
        {
            Direction = direction;
            ScriptFolder = scriptFolder;
            TargetSchema = targetSchema;
            TargetTablespace = targetTablespace;
            TargetEnvironment = targetEnvironment;
            TargetVersion = targetVersion;
            UseStored = useStored;
            Forced = forced;
        }

        public MigrationDirection Direction { get; private set; }
        public string ScriptFolder { get; private set; }

        public string TargetSchema { get; private set; }
        public string TargetTablespace { get; private set; }
        public IList<string> TargetEnvironment { get; private set; }
        public long? TargetVersion { get; private set; }

        public bool UseStored { get; private set; }
        public bool Forced { get; private set; }
    }
}
