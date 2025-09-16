using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

class Program
{
    const string KeyFileName = "usbkey.txt";
    static string? keyContent = null;

    static void Main()
    {
        Console.WriteLine("USB Keyfile Utility");
        Console.WriteLine("1. Create keyfile on USB drive");
        Console.WriteLine("2. Start monitoring for USB keyfile");
        Console.Write("Select option (1/2): ");
        var input = Console.ReadLine();
        if (input == "1")
        {
            CreateKeyfileOnUsb();
        }
        else if (input == "2")
        {
            MonitorUsbForKeyfile();
        }
        else
        {
            Console.WriteLine("Invalid option.");
        }
    }

    static void CreateKeyfileOnUsb()
    {
        while (true)
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => {
                    try 
                    {
                        return d.IsReady && IsLikelyUsbDrive(d);
                    }
                    catch 
                    {
                        return false; // Skip drives that can't be accessed safely
                    }
                })
                .ToArray();
            
            if (drives.Length == 0)
            {
                Console.WriteLine("No USB drives detected. Please insert a USB stick...");
                System.Threading.Thread.Sleep(2000);
                continue;
            }
            
            Console.WriteLine("Available USB drives:");
            for (int i = 0; i < drives.Length; i++)
            {
                string driveLabel = !string.IsNullOrEmpty(drives[i].VolumeLabel) ? drives[i].VolumeLabel : "Unlabeled";
                string driveTypeInfo = $"[{drives[i].DriveType}]";
                
                if (drives[i].IsReady)
                {
                    try
                    {
                        long totalGB = drives[i].TotalSize / (1024 * 1024 * 1024);
                        long freeGB = drives[i].TotalFreeSpace / (1024 * 1024 * 1024);
                        Console.WriteLine($"{i + 1}. {drives[i].Name} \"{driveLabel}\" {driveTypeInfo} ({totalGB}GB total, {freeGB}GB free)");
                    }
                    catch
                    {
                        Console.WriteLine($"{i + 1}. {drives[i].Name} \"{driveLabel}\" {driveTypeInfo}");
                    }
                }
                else
                {
                    Console.WriteLine($"{i + 1}. {drives[i].Name} \"{driveLabel}\" {driveTypeInfo} [Not Ready]");
                }
            }
            
            Console.Write("Select drive number: ");
            if (!int.TryParse(Console.ReadLine(), out int driveIndex) || driveIndex < 1 || driveIndex > drives.Length)
            {
                Console.WriteLine("Invalid selection.");
                continue;
            }
            
            var selectedDrive = drives[driveIndex - 1];
            
            // Get user's name for the mission
            Console.Write("Enter your name for the mission: ");
            string userName = Console.ReadLine()?.Trim() ?? "Agent";
            if (string.IsNullOrEmpty(userName))
            {
                userName = "Agent";
            }
            
            keyContent = Guid.NewGuid().ToString("N") + new Random().Next(100000, 999999);
            
            // Combine name and key content with a separator
            string dataToEncrypt = $"{userName}|{keyContent}";
            
            string password = ReadPasswordWithMinLength("Enter a password for the keyfile (min 5 chars): ", 5);
            var keyFilePath = Path.Combine(selectedDrive.RootDirectory.FullName, KeyFileName);
            
            // Encrypt combined data with password
            var encrypted = EncryptString(dataToEncrypt, password);
            
            // Write file safely with proper verification
            try 
            {
                // Write the file with enhanced synchronization
                using (var fs = new FileStream(keyFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(encrypted, 0, encrypted.Length);
                    fs.Flush(true); // Force write to disk
                }
                
                // Give the filesystem extra time to complete all operations before verification
                System.Threading.Thread.Sleep(1000);
                
                // Verify file was written correctly by reading it back
                System.Threading.Thread.Sleep(500); // Give filesystem time to complete
                
                if (File.Exists(keyFilePath)) 
                {
                    var fileInfo = new FileInfo(keyFilePath);
                    if (fileInfo.Length == encrypted.Length)
                    {
                        // Additional verification: try to read and decrypt
                        try
                        {
                            var testRead = File.ReadAllBytes(keyFilePath);
                            var testDecrypt = DecryptString(testRead, password);
                            
                            // Parse the decrypted data to extract name and key
                            if (!string.IsNullOrEmpty(testDecrypt) && testDecrypt.Contains("|"))
                            {
                                var parts = testDecrypt.Split('|');
                                if (parts.Length == 2 && parts[1] == keyContent)
                                {
                                    Console.WriteLine($"✓ Keyfile created successfully at {keyFilePath}");
                                    Console.WriteLine($"✓ File size: {fileInfo.Length} bytes");
                                    Console.WriteLine($"✓ Encryption verified for {parts[0]}");
                                    Console.WriteLine("\nIMPORTANT: Remember your password!");
                                    
                                    Console.WriteLine("\n🔒 To avoid corruption, please safely eject the USB drive manually:");
                                    
                                    if (OperatingSystem.IsWindows())
                                    {
                                        Console.WriteLine("• Windows: Click the 'Safely Remove Hardware' icon in the system tray");
                                        Console.WriteLine("• Or right-click the USB drive in File Explorer and select 'Eject'");
                                        Console.WriteLine("• Wait for the 'Safe to Remove Hardware' message");
                                    }
                                    else if (OperatingSystem.IsMacOS())
                                    {
                                        Console.WriteLine("• macOS: Right-click the drive on Desktop and select 'Eject'");
                                        Console.WriteLine("• Or drag the drive icon to the Trash");
                                    }
                                    else
                                    {
                                        Console.WriteLine("• Linux: Right-click the drive and select 'Unmount' or 'Eject'");
                                        Console.WriteLine("• Or use: umount /path/to/drive");
                                    }
                                    
                                    Console.WriteLine("\n⚠️  NEVER remove the USB drive while the program is writing to it!");
                                    Console.WriteLine("📱 This keyfile will work on Windows, macOS, and Linux systems.");
                                    
                                    Console.WriteLine("\nPress any key when you have safely ejected the USB drive...");
                                    Console.ReadKey();
                                    break;
                                }
                                else
                                {
                                    Console.WriteLine("❌ Verification failed: key content doesn't match");
                                    File.Delete(keyFilePath);
                                    continue;
                                }
                            }
                            else
                            {
                                Console.WriteLine("❌ Verification failed: invalid data format");
                                File.Delete(keyFilePath);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Verification failed: {ex.Message}");
                            File.Delete(keyFilePath);
                            continue;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ File size mismatch. Expected {encrypted.Length} bytes, got {fileInfo.Length}");
                        File.Delete(keyFilePath);
                        continue;
                    }
                }
                else
                {
                    Console.WriteLine("❌ Failed to create keyfile");
                    continue;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error writing to USB drive: {ex.Message}");
                Console.WriteLine("Please check if the drive has write protection or try a different drive.");
                continue;
            }
        }
    }

    static void MonitorUsbForKeyfile()
    {
        bool passwordValid = false;
        string? password = null;
        bool monitoringMessageShown = false;
        
        while (true)
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => {
                    try 
                    {
                        return d.IsReady && IsLikelyUsbDrive(d);
                    }
                    catch 
                    {
                        return false; // Skip drives that can't be accessed safely
                    }
                })
                .ToArray();
            
            if (drives.Length == 0)
            {
                if (!monitoringMessageShown)
                {
                    Console.WriteLine("Monitoring for USB keyfile. Insert your USB stick...");
                    monitoringMessageShown = true;
                }
                passwordValid = false; // Reset password validation when no drives
                password = null;
                System.Threading.Thread.Sleep(2000);
                continue;
            }
            
            monitoringMessageShown = false;
            
            foreach (var drive in drives)
            {
                var keyFilePath = Path.Combine(drive.RootDirectory.FullName, KeyFileName);
                if (File.Exists(keyFilePath))
                {
                    while (!passwordValid)
                    {
                        // Check if USB is still present before asking for password
                        // Use safer method that doesn't stress the filesystem
                        try
                        {
                            var checkDrives = DriveInfo.GetDrives()
                                .Where(d => {
                                    try 
                                    {
                                        return IsLikelyUsbDrive(d) && d.Name == drive.Name;
                                    }
                                    catch 
                                    {
                                        return false; // Drive likely removed
                                    }
                                })
                                .ToArray();
                                
                            if (checkDrives.Length == 0)
                            {
                                PrintUsbRemovedAscii();
                                Console.WriteLine("USB removed. Please reinsert and try again.");
                                passwordValid = false;
                                password = null;
                                break;
                            }
                        }
                        catch
                        {
                            // If drive checking fails, assume USB was removed
                            PrintUsbRemovedAscii();
                            Console.WriteLine("USB access error. Please reinsert and try again.");
                            passwordValid = false;
                            password = null;
                            break;
                        }
                        
                        if (password == null)
                        {
                            Console.Write("Enter password to unlock keyfile: ");
                            password = ReadPassword();
                        }
                        
                        try
                        {
                            // Safe file reading
                            byte[] encrypted;
                            using (var fs = new FileStream(keyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                encrypted = new byte[fs.Length];
                                int totalRead = 0;
                                while (totalRead < encrypted.Length)
                                {
                                    int bytesRead = fs.Read(encrypted, totalRead, encrypted.Length - totalRead);
                                    if (bytesRead == 0) break;
                                    totalRead += bytesRead;
                                }
                            }
                            
                            var decrypted = DecryptString(encrypted, password);
                            if (!string.IsNullOrEmpty(decrypted) && decrypted.Contains("|"))
                            {
                                var parts = decrypted.Split('|');
                                if (parts.Length == 2)
                                {
                                    string userName = parts[0];
                                    string extractedKey = parts[1];
                                    
                                    Console.WriteLine($"✓ Correct USB key detected for {userName}!");
                                    
                                    // Use the extracted name in the mission message
                                    Speak($@"{userName},
Your mission, should you choose to accept it, is to locate the key to the Mystery Room hidden somewhere inside the school building. Clues may be disguised in ordinary objects — classroom doors, old schedules, even forgotten lockers.

Once inside, you must identify the true owner of the mystical code. This code holds the power to unlock the path toward the next exam challenge.

Proceed with caution, trust no one, and remember: observation and clever thinking will be your greatest allies.

As always, should you or your team be caught or fail, the Secretary will disavow any knowledge of your actions.");
                                    passwordValid = true;
                                }
                                else
                                {
                                    Console.WriteLine("❌ Invalid keyfile format. Try again.");
                                    password = null;
                                }
                            }
                            else
                            {
                                Console.WriteLine("❌ Wrong password or corrupted keyfile. Try again.");
                                password = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Error reading keyfile: {ex.Message}");
                            Console.WriteLine("Please try again or check if the file is corrupted.");
                            password = null;
                        }
                    }
                    
                    if (passwordValid) 
                    {
                        // Monitor for USB removal
                        Console.WriteLine("\n🔑 USB key authenticated successfully!");
                        Console.WriteLine("Keep the USB inserted to maintain access.");
                        Console.WriteLine("Removing the USB will trigger self-destruct sequence...");
                        
                        // Store the drive name for safer checking
                        string authenticatedDriveName = drive.Name;
                        int checkCounter = 0; // Counter to reduce file existence checks
                        
                        while (true)
                        {
                            System.Threading.Thread.Sleep(2000); // Longer interval to reduce filesystem stress
                            checkCounter++;
                            
                            try
                            {
                                // Safer USB detection: only check drive names without accessing IsReady
                                var currentDrives = DriveInfo.GetDrives()
                                    .Where(d => {
                                        try 
                                        {
                                            return IsLikelyUsbDrive(d) && d.Name == authenticatedDriveName;
                                        }
                                        catch 
                                        {
                                            return false; // Drive is likely removed/corrupted
                                        }
                                    })
                                    .ToArray();
                                
                                // If no matching drive found, USB was removed
                                if (currentDrives.Length == 0)
                                {
                                    Console.WriteLine();
                                    Speak("This message will self-destruct in five seconds.");
                                    
                                    for (int i = 5; i >= 1; i--)
                                    {
                                        Console.WriteLine($"🔥 Auto-destroy in {i}...");
                                        BeepBomb();
                                        System.Threading.Thread.Sleep(1000);
                                    }
                                    
                                    Console.WriteLine("💥 Mission complete. Goodbye!");
                                    return; // Clean exit
                                }
                                
                                // Only check file existence every 5th iteration (every 10 seconds) to minimize filesystem stress
                                if (currentDrives.Length > 0 && checkCounter % 5 == 0)
                                {
                                    var checkKeyFilePath = Path.Combine(authenticatedDriveName, KeyFileName);
                                    if (!File.Exists(checkKeyFilePath))
                                    {
                                        Console.WriteLine("\n⚠️  Keyfile no longer accessible. USB may have been corrupted or removed improperly.");
                                        Console.WriteLine("💥 Mission terminated due to keyfile corruption.");
                                        return;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // If any exception occurs (drive removed, access denied, etc.), assume USB is gone
                                Console.WriteLine($"\n⚠️  USB access error: {ex.Message}");
                                Console.WriteLine();
                                Speak("This message will self-destruct in five seconds.");
                                
                                for (int i = 5; i >= 1; i--)
                                {
                                    Console.WriteLine($"🔥 Auto-destroy in {i}...");
                                    BeepBomb();
                                    System.Threading.Thread.Sleep(1000);
                                }
                                
                                Console.WriteLine("💥 Mission complete. Goodbye!");
                                return;
                            }
                        }
                    }
                }
            }
            
            System.Threading.Thread.Sleep(1000);
        }
    }

    // Helper: Detect likely USB drive (cross-platform)
    static bool IsLikelyUsbDrive(DriveInfo d)
    {
        try
        {
            // On macOS, USB drives are usually mounted under /Volumes
            if (Environment.OSVersion.Platform == PlatformID.Unix && d.RootDirectory.FullName.StartsWith("/Volumes/"))
            {
                return d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Unknown;
            }
            
            // On Windows, check for removable drives and also check typical USB characteristics
            if (OperatingSystem.IsWindows())
            {
                // Primary check: Removable drives
                if (d.DriveType == DriveType.Removable)
                    return true;
                
                // Secondary check: Fixed drives that might be USB (some USB drives show as Fixed)
                if (d.DriveType == DriveType.Fixed)
                {
                    // Additional heuristics for Windows USB detection
                    // Check if it's not the system drive (usually C:)
                    string systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
                    if (!d.Name.StartsWith(systemDrive, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                
                return false;
            }
            
            // On Linux and other platforms, use Removable
            return d.DriveType == DriveType.Removable;
        }
        catch
        {
            // If any error occurs during detection, err on the side of caution
            return false;
        }
    }

    // Cross-platform voice message
    static void Speak(string message)
    {
        // Always display the message text
        Console.WriteLine($"\n🔊 [MISSION BRIEFING]:");
        Console.WriteLine(message);
        Console.WriteLine(new string('=', 60));
        
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "say",
                    Arguments = $"\"{message.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(processInfo);
                // Don't wait for completion to avoid blocking
            }
            else if (OperatingSystem.IsWindows())
            {
                // Use Windows Speech API via PowerShell for better compatibility
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"Add-Type -AssemblyName System.Speech; $speak = New-Object System.Speech.Synthesis.SpeechSynthesizer; $speak.Speak('{message.Replace("'", "''")}')\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var process = Process.Start(processInfo);
                // Don't wait for completion to avoid blocking
            }
            else
            {
                // For Linux and other platforms, just display
                Console.WriteLine("💬 [Text-to-speech not available on this platform]");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💬 [Voice synthesis failed: {ex.Message}]");
        }
    }

    // Cross-platform beep
    static void BeepBomb()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Multiple beeps for dramatic effect on Windows
                Console.Beep(1000, 150);
                System.Threading.Thread.Sleep(50);
                Console.Beep(800, 150);
            }
            else if (OperatingSystem.IsMacOS())
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "afplay",
                    Arguments = "/System/Library/Sounds/Glass.aiff",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(processInfo);
                // Don't wait to avoid slowing down countdown
            }
            else
            {
                // Bell character for Linux and other platforms
                Console.Write("\a");
                System.Threading.Thread.Sleep(100);
                Console.Write("\a");
            }
        }
        catch
        {
            // Fallback to bell character
            Console.Write("\a");
        }
    }
    
    // ASCII warning for USB removal
    static void PrintUsbRemovedAscii()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("██████████████████████████████████████████████████████████████████");
        Console.WriteLine("██                                                              ██");
        Console.WriteLine("██  ⚠️  ⚠️  ⚠️   SECURITY BREACH DETECTED   ⚠️  ⚠️  ⚠️   ██");
        Console.WriteLine("██                                                              ██");
        Console.WriteLine("██        USB SECURITY KEY REMOVED UNEXPECTEDLY!               ██");
        Console.WriteLine("██                                                              ██");
        Console.WriteLine("██  ████  ████  ████   ████   ████  ████   ████   ████        ██");
        Console.WriteLine("██  ████  ████  ████   ████   ████  ████   ████   ████        ██");
        Console.WriteLine("██                                                              ██");
        Console.WriteLine("██           🚨 MISSION COMPROMISED 🚨                         ██");
        Console.WriteLine("██                                                              ██");
        Console.WriteLine("██     PLEASE REINSERT USB KEY TO CONTINUE OPERATIONS          ██");
        Console.WriteLine("██                                                              ██");
        Console.WriteLine("██  ████  ████  ████   ████   ████  ████   ████   ████        ██");
        Console.WriteLine("██  ████  ████  ████   ████   ████  ████   ████   ████        ██");
        Console.WriteLine("██                                                              ██");
        Console.WriteLine("██████████████████████████████████████████████████████████████████");
        Console.ResetColor();
        Console.WriteLine();
        
        // Additional dramatic effect
        for (int i = 0; i < 3; i++)
        {
            Console.Write("⚠️ ");
            System.Threading.Thread.Sleep(200);
        }
        Console.WriteLine();
    }

    // Read password with minimum length
    static string ReadPasswordWithMinLength(string prompt, int minLength)
    {
        string password;
        do
        {
            Console.Write(prompt);
            password = ReadPassword();
            if (password.Length < minLength)
            {
                Console.WriteLine($"❌ Password must be at least {minLength} characters.");
            }
        } while (password.Length < minLength);
        return password;
    }

    // Read password (no echo)
    static string ReadPassword()
    {
        var pwd = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && pwd.Length > 0)
            {
                pwd.Length--;
                continue;
            }
            if (!char.IsControl(key.KeyChar))
                pwd.Append(key.KeyChar);
        }
        Console.WriteLine();
        return pwd.ToString();
    }

    // AES encryption
    static byte[] EncryptString(string plainText, string password)
    {
        using var aes = Aes.Create();
        var salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(salt);
        
        var key = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        aes.Key = key.GetBytes(32);
        aes.GenerateIV();
        
        using var ms = new MemoryStream();
        ms.Write(salt, 0, salt.Length); // prepend salt
        ms.Write(aes.IV, 0, aes.IV.Length); // prepend IV
        
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
            sw.Flush();
        }
        
        return ms.ToArray();
    }

    // AES decryption
    static string DecryptString(byte[] cipherData, string password)
    {
        try
        {
            if (cipherData.Length < 32) return string.Empty; // Too short for salt + IV
            
            using var ms = new MemoryStream(cipherData);
            var salt = new byte[16];
            var iv = new byte[16];
            
            if (ms.Read(salt, 0, 16) != 16) return string.Empty;
            if (ms.Read(iv, 0, 16) != 16) return string.Empty;
            
            var key = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            using var aes = Aes.Create();
            aes.Key = key.GetBytes(32);
            aes.IV = iv;
            
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }
}
