namespace FModel.ModTools.Tests;

internal sealed class TestDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FModelRecreateTests", Guid.NewGuid().ToString("N"));

    public TestDirectory() => Directory.CreateDirectory(Path);

    public string Combine(params string[] parts) => parts.Aggregate(Path, System.IO.Path.Combine);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
        catch (IOException)
        {
            // Windows antivirus/indexing can briefly hold test files. The OS temp cleaner can remove them later.
        }
    }
}
