using System;
using System.IO;
using Root.Core.Services;
using Xunit;

namespace Root.Tests;

public class SystemProtectionServiceTests
{
    [Fact]
    public void IsProtectedPath_ProtectsWindowsAndSystem32Directories()
    {
        var service = new SystemProtectionService();

        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        Assert.True(service.IsProtectedPath(windir));
        Assert.True(service.IsProtectedPath(Path.Combine(windir, "explorer.exe")));
        Assert.True(service.IsProtectedPath(system32));
        Assert.True(service.IsProtectedPath(Path.Combine(system32, "kernel32.dll")));
        Assert.True(service.IsProtectedPath(programFiles));
        Assert.True(service.IsProtectedPath(Path.Combine(programFiles, "Common Files", "test.txt")));
    }

    [Fact]
    public void IsProtectedPath_ProtectsRootSystemMetadataAndFiles()
    {
        var service = new SystemProtectionService();

        Assert.True(service.IsProtectedPath(@"C:\$Recycle.Bin\S-1-5-18"));
        Assert.True(service.IsProtectedPath(@"C:\System Volume Information\WPSettings.dat"));
        Assert.True(service.IsProtectedPath(@"C:\Recovery\WindowsRE\Winre.wim"));
        Assert.True(service.IsProtectedPath(@"C:\pagefile.sys"));
        Assert.True(service.IsProtectedPath(@"C:\swapfile.sys"));
        Assert.True(service.IsProtectedPath(@"C:\hiberfil.sys"));
        Assert.True(service.IsProtectedPath(@"C:\bootmgr"));
    }

    [Fact]
    public void IsProtectedExtension_BlocksExecutableAndSystemBinaryExtensions()
    {
        var service = new SystemProtectionService();

        Assert.True(service.IsProtectedExtension(@"D:\Temp\driver.sys"));
        Assert.True(service.IsProtectedExtension(@"D:\Temp\library.dll"));
        Assert.True(service.IsProtectedExtension(@"D:\Temp\program.exe"));
        Assert.True(service.IsProtectedExtension(@"D:\Temp\installer.msi"));
        Assert.True(service.IsProtectedExtension(@"D:\Temp\script.bat"));
        Assert.True(service.IsProtectedExtension(@"D:\Temp\script.ps1"));
        Assert.True(service.IsProtectedExtension(@"D:\Temp\registry.reg"));

        // Ekstensi non-eksekutabel
        Assert.False(service.IsProtectedExtension(@"D:\Temp\cache.tmp"));
        Assert.False(service.IsProtectedExtension(@"D:\Temp\crash.dmp"));
        Assert.False(service.IsProtectedExtension(@"D:\Temp\app.log"));
    }

    [Fact]
    public void IsProtectedPath_ProtectsRegistryHives()
    {
        var service = new SystemProtectionService();

        Assert.True(service.IsProtectedPath(@"C:\Users\Faza\NTUSER.DAT"));
        Assert.True(service.IsProtectedPath(@"C:\Users\Faza\NTUSER.DAT{12345678-1234}.TM.blf"));
        Assert.True(service.IsProtectedPath(@"C:\Users\Faza\AppData\Local\Microsoft\Windows\UsrClass.dat"));
    }

    [Fact]
    public void IsProtectedPath_AllowsSafeNonSystemUserDirectories()
    {
        var service = new SystemProtectionService();

        string userFolder = @"D:\Projects\MyApp\temp.tmp";
        Assert.False(service.IsProtectedPath(userFolder));
        Assert.Null(service.GetProtectionReason(userFolder));
    }

    [Fact]
    public void CustomProtectedRoots_CanBeInjectedForTesting()
    {
        string fakeSystemDir = @"C:\FakeSystemCustomDir";
        var service = new SystemProtectionService([fakeSystemDir]);

        Assert.True(service.IsProtectedPath(fakeSystemDir));
        Assert.True(service.IsProtectedPath(Path.Combine(fakeSystemDir, "subfolder", "data.tmp")));
        Assert.NotNull(service.GetProtectionReason(Path.Combine(fakeSystemDir, "subfolder", "data.tmp")));
    }
}
