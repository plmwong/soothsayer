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

        private readonly IDictionary<string, object> _options;

        public OptionsFile(string optionsPath)
        {
            if (File.Exists(optionsPath))
            {
                var optionsFileContents = File.ReadAllText(optionsPath);
                _options = JsonConvert.DeserializeObject<Dictionary<string, object>>(optionsFileContents);

                Output.Info("Options file contains the following configurations:");

                foreach (var @override in _options)
                {
                    Output.Text("  '{0}' : '{1}'".FormatWith(@override.Key, @override.Value));
                }
            }
            else
            {
                _options = new Dictionary<string, object>();
            }
        }

        public OptionsFile()
        {
            _options = new Dictionary<string, object>();
        }

        public void ApplyTo(IOptions options)
        {
            var optionsType = options.GetType();
            var optionsProperties = optionsType.GetProperties();

            foreach (var property in optionsProperties)
            {
                foreach (var option in _options)
                {
                    if (property.Name.ToLowerInvariant() == option.Key.ToLowerInvariant())
                    {
                        property.SetValue(options, option.Value);
                    }
                }
            }
        }

        public bool IsEmpty()
        {
            return _options.Count == 0;
        }
    }
}