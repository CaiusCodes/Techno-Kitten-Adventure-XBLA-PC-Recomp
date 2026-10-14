using System.Text.Json.Nodes;

namespace Tka.Installer;

internal static class PlayExtras
{
    public static void ApplyFullscreen(string root)
    {
        var directory = Path.Combine(PortableLayout.Data(root),"userdata/settings");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory,"display.json");
        var settings = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : new JsonObject();
        if (settings is null) throw new InvalidDataException("Existing display settings aren't a JSON object; they haven't been replaced.");
        settings["Fullscreen"] = true;
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp,settings.ToJsonString());
        File.Move(temp,path,true);
    }

    public static string ShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Techno Kitten Adventure.lnk");
    public static void CreateShortcut(string root, string path)
    {
        if (File.Exists(path)) throw new IOException("The shortcut already exists and has not been overwritten.");
        // Windows' built-in shell COM component; no extra library or registry installation.
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("Windows shortcut support is unavailable.");
        object? shell = null, shortcut = null;
        var temporary = Path.Combine(Path.GetDirectoryName(path)!,".tka-shortcut-"+Guid.NewGuid().ToString("N")+".lnk");
        try
        {
            shell = Activator.CreateInstance(type)!;
            shortcut = type.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [temporary]);
            var target = Path.Combine(root,"Techno Kitten Adventure.exe");
            var member = shortcut!.GetType();
            foreach (var (name,value) in new[] { ("TargetPath",target), ("WorkingDirectory",root), ("IconLocation",target+",0"), ("Description","Techno Kitten Adventure") })
                member.InvokeMember(name,System.Reflection.BindingFlags.SetProperty,null,shortcut,[value]);
            // A concurrent file creation must not silently overwrite another shortcut.
            if (File.Exists(path)) throw new IOException("A shortcut already exists; it was left unchanged.");
            member.InvokeMember("Save",System.Reflection.BindingFlags.InvokeMethod,null,shortcut,[]);
            File.Move(temporary,path,false);
        }
        finally
        {
            if (shortcut is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
