namespace Nebuctl;

public static class Ex
{
    public static string File(this DirectoryInfo dir, string file) => new FileInfo(JoinPath(dir.ToString(), file)).ToString();

    public static string JoinPath(string dir, string file)
    {
        if (string.IsNullOrEmpty(dir))
            return file;

        if (dir.EndsWith("/") || dir.EndsWith("\\"))
            return dir + file;

        return dir + '/' + file;
    }
}