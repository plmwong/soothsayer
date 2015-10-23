using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;
using soothsayer.Infrastructure;
using soothsayer.Infrastructure.IO;

namespace soothsayer.Commands
{
    public abstract class DatabaseCommand<T> : ICommand<T> where T : IOptions, new()
    {
        private readonly ISecureConsole _secureConsole;

        protected DatabaseCommand(ISecureConsole secureConsole)
        {
            _secureConsole = secureConsole;
        }

        public abstract string CommandText { get; }
        public abstract string Description { get; }

        public T Options { get { return new T(); } }
        protected abstract void ExecuteCore(string[] arguments, T options);

        public void Execute(IEnumerable<string> arguments, OptionsFile overrides)
        {
            var options = Options;
            var argumentsArray = arguments.ToArray();

            var argumentsWithOverrides = overrides.ApplyTo(argumentsArray);

            if (Parser.Default.ParseArguments(argumentsWithOverrides, options))
            {
                ExecuteCore(argumentsArray, options);
            }
        }

        protected string GetOraclePassword(IDatabaseCommandOptions options)
        {
            var password = options.Password;
            if (password.IsNullOrEmpty())
            {
                Console.WriteLine("Enter the password for '{0}': ", options.Username);
                password = _secureConsole.ReadLine('*');
            }

            return password;
        }
    }
}
