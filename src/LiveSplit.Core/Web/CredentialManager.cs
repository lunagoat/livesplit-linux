/*

MIT License

Copyright(c) 2017 Gérald Barré

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using LiveSplit.Options;
using LiveSplit.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace LiveSplit.Web;

public interface ICredentialStore
{
    Credential Read(string applicationName);
    bool Write(string applicationName, string userName, string secret);
    bool Delete(string applicationName);
}

public static class CredentialManager
{
    private static ICredentialStore store;

    public static ICredentialStore Store
    {
        get => store ??= CreateDefaultStore();
        set => store = value;
    }

    private static ICredentialStore CreateDefaultStore()
    {
        var file = new FileCredentialStore(Path.Combine(Paths.ConfigDirectory, "credentials.json"));
        return SecretToolCredentialStore.IsAvailable
            ? new FallbackCredentialStore(new SecretToolCredentialStore(), file)
            : file;
    }

    public static Credential ReadCredential(string applicationName)
    {
        return Store.Read(applicationName);
    }

    public static void WriteCredential(string applicationName, string userName, string secret)
    {
        if (applicationName == null)
        {
            throw new ArgumentNullException(nameof(applicationName));
        }

        if (!Store.Write(applicationName, userName ?? "", secret ?? ""))
        {
            throw new InvalidOperationException("Unable to store the credential.");
        }
    }

    public static bool CredentialExists(string applicationName)
    {
        return ReadCredential(applicationName) != null;
    }

    public static void DeleteCredential(string applicationName)
    {
        Store.Delete(applicationName);
    }
}

public sealed class FallbackCredentialStore : ICredentialStore
{
    private readonly ICredentialStore primary;
    private readonly ICredentialStore fallback;

    public FallbackCredentialStore(ICredentialStore primary, ICredentialStore fallback)
    {
        this.primary = primary;
        this.fallback = fallback;
    }

    public Credential Read(string applicationName)
    {
        return primary.Read(applicationName) ?? fallback.Read(applicationName);
    }

    public bool Write(string applicationName, string userName, string secret)
    {
        if (primary.Write(applicationName, userName, secret))
        {
            fallback.Delete(applicationName);
            return true;
        }

        return fallback.Write(applicationName, userName, secret);
    }

    public bool Delete(string applicationName)
    {
        bool a = primary.Delete(applicationName);
        bool b = fallback.Delete(applicationName);
        return a || b;
    }
}

public sealed class FileCredentialStore : ICredentialStore
{
    private static readonly object SyncRoot = new();
    private readonly string path;

    public FileCredentialStore(string path)
    {
        this.path = path;
    }

    private sealed class Entry
    {
        public string UserName { get; set; }
        public string Secret { get; set; }
    }

    private Dictionary<string, Entry> Load()
    {
        try
        {
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path)) ?? [];
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        return [];
    }

    private bool Save(Dictionary<string, Entry> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            var options = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            };
            using (var stream = new FileStream(temp, options))
            {
                stream.Write(JsonSerializer.SerializeToUtf8Bytes(entries));
            }

            File.Move(temp, path, true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }

    public Credential Read(string applicationName)
    {
        lock (SyncRoot)
        {
            return Load().TryGetValue(applicationName, out Entry entry)
                ? new Credential(CredentialType.Generic, applicationName, entry.UserName, entry.Secret)
                : null;
        }
    }

    public bool Write(string applicationName, string userName, string secret)
    {
        lock (SyncRoot)
        {
            Dictionary<string, Entry> entries = Load();
            entries[applicationName] = new Entry { UserName = userName, Secret = secret };
            return Save(entries);
        }
    }

    public bool Delete(string applicationName)
    {
        lock (SyncRoot)
        {
            Dictionary<string, Entry> entries = Load();
            return entries.Remove(applicationName) && Save(entries);
        }
    }
}

public sealed class SecretToolCredentialStore : ICredentialStore
{
    private const string Executable = "secret-tool";

    private sealed class Payload
    {
        public string UserName { get; set; }
        public string Secret { get; set; }
    }

    public static bool IsAvailable
    {
        get
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            foreach (string dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (File.Exists(Path.Combine(dir, Executable)))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public Credential Read(string applicationName)
    {
        if (!Run(["lookup", "application", "livesplit", "name", applicationName], null, out string output))
        {
            return null;
        }

        try
        {
            Payload payload = JsonSerializer.Deserialize<Payload>(output);
            return payload == null
                ? null
                : new Credential(CredentialType.Generic, applicationName, payload.UserName, payload.Secret);
        }
        catch
        {
            return null;
        }
    }

    public bool Write(string applicationName, string userName, string secret)
    {
        string json = JsonSerializer.Serialize(new Payload { UserName = userName, Secret = secret });
        return Run(["store", "--label=LiveSplit: " + applicationName, "application", "livesplit", "name", applicationName], json, out _);
    }

    public bool Delete(string applicationName)
    {
        return Run(["clear", "application", "livesplit", "name", applicationName], null, out _);
    }

    private static bool Run(string[] args, string stdin, out string output)
    {
        output = null;
        try
        {
            var startInfo = new ProcessStartInfo(Executable)
            {
                RedirectStandardInput = stdin != null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
            };
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using Process process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            if (stdin != null)
            {
                process.StandardInput.Write(stdin);
                process.StandardInput.Close();
            }

            string stderr = process.StandardError.ReadToEndAsync().GetAwaiter().GetResult();
            output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(10000))
            {
                process.Kill();
                return false;
            }

            return process.ExitCode == 0 && (args[0] != "lookup" || output.Length > 0);
        }
        catch
        {
            return false;
        }
    }
}

public enum CredentialType
{
    Generic = 1,
    DomainPassword = 2,
    DomainCertificate = 3,
    DomainVisiblePassword = 4,
    GenericCertificate = 5,
    DomainExtended = 6,
    Maximum = 7,
    MaximumEx = Maximum + 1000,
}

public class Credential
{
    public CredentialType CredentialType { get; }

    public string ApplicationName { get; }

    public string UserName { get; }

    public string Password { get; }

    public Credential(CredentialType credentialType, string applicationName, string userName, string password)
    {
        ApplicationName = applicationName;
        UserName = userName;
        Password = password;
        CredentialType = credentialType;
    }

    public override string ToString()
    {
        return string.Format("CredentialType: {0}, ApplicationName: {1}, UserName: {2}, Password: {3}", CredentialType, ApplicationName, UserName, "********");
    }
}
