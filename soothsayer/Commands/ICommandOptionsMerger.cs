namespace soothsayer.Commands
{
    public interface ICommandOptionsMerger
    {
        void Merge<T>(T baseOptions, T overrideOptions) where T : IOptions;
    }
}