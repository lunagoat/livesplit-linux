namespace LiveSplit.Hosting;

public interface IFileAssociation
{
    bool IsRegistered { get; }

    void Register();

    void Unregister();
}

public sealed class NoOpFileAssociation : IFileAssociation
{
    public bool IsRegistered => true;

    public void Register()
    {
    }

    public void Unregister()
    {
    }
}

public static class FileAssociation
{
    public static IFileAssociation Current { get; set; } = new NoOpFileAssociation();
}
