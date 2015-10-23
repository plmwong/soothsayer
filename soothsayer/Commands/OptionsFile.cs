using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using soothsayer.Infrastructure;
using soothsayer.Infrastructure.IO;

namespace soothsayer.Commands
{
    public class OptionsFile
    {
        public const string Name = "options.json";

        private readonly IDictionary<string, string> _options;

        public OptionsFile(string optionsPath)
        {
            if (File.Exists(optionsPath))
            {
                var optionsFileContents = File.ReadAllText(optionsPath);
                _options = JsonConvert.DeserializeObject<Dictionary<string, string>>(optionsFileContents);

                Output.Info("Options file contains the following configurations:");

                foreach (var @override in _options)
                {
                    Output.Text("  '{0}'\t: '{1}'".FormatWith(@override.Key, @override.Value));
                }
            }
            else
            {
                _options = new Dictionary<string, string>();
            }
        }

        public OptionsFile()
        {
            _options = new Dictionary<string, string>();
        }

        public string[] ApplyTo(string[] arguments)
        {
            var newArguments = new string[arguments.Length + 2 * _options.Count];
            var index = arguments.Length;

            foreach (var option in _options)
            {
                newArguments[index++] = "--{0}".FormatWith(option.Key);
                newArguments[index++] = option.Value;
            }

            return newArguments;
        }

        public bool IsEmpty()
        {
            return _options.Count == 0;
        }
    }
}