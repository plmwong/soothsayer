using System.Reflection;

namespace soothsayer.Commands
{
    public class CommandOptionsMerger : ICommandOptionsMerger
    {
        public void Merge<T>(T baseOptions, T overrideOptions) where T : IOptions
        {
            var optionsType = typeof (T);
            var publicProperties = optionsType.GetProperties(BindingFlags.Public);

            foreach (var property in publicProperties)
            {
                property.SetValue(baseOptions, property.GetValue(overrideOptions));
            }
        }
    }
}