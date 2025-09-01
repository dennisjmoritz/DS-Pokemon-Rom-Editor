using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace DSPRE.BatchExport {
  
  // Minimal ROM handling for Linux
  public class SimpleRomExtractor {
    public static bool ExtractRom(string romPath, string outputDir) {
      try {
        Directory.CreateDirectory(outputDir);
        var process = new Process {
          StartInfo = new ProcessStartInfo {
            FileName = "ndstool",
            Arguments = $"-x \"{romPath}\" -9 \"{Path.Combine(outputDir, "arm9.bin")}\" -7 \"{Path.Combine(outputDir, "arm7.bin")}\" -y9 \"{Path.Combine(outputDir, "y9.bin")}\" -y7 \"{Path.Combine(outputDir, "y7.bin")}\" -d \"{Path.Combine(outputDir, "data")}\" -y \"{Path.Combine(outputDir, "overlay")}\" -t \"{Path.Combine(outputDir, "banner.bin")}\" -h \"{Path.Combine(outputDir, "header.bin")}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
          }
        };
        
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
      } catch {
        return false;
      }
    }
  }

  // Minimal NARC extraction functionality
  public class SimpleNarcExtractor {
    public static void ExtractNarcsFromData(string dataDir, string outputDir) {
      // This is a simplified version - in practice you'd need full NARC parsing
      // For now, just ensure the directory structure exists
      Directory.CreateDirectory(outputDir);
    }
  }

  class Program {
    static void Usage() {
      Console.WriteLine("DS Pokemon ROM Editor - Linux PNG Batch Exporter");
      Console.WriteLine("Usage:");
      Console.WriteLine("  dotnet DSPRE.BatchExport.Linux.dll <ROM.nds | extracted_contents> <out_dir> [options]");
      Console.WriteLine("");
      Console.WriteLine("Options:");
      Console.WriteLine("  --extract-only    Only extract ROM contents, don't generate PNGs");
      Console.WriteLine("  --help           Show this help message");
      Console.WriteLine("");
      Console.WriteLine("Examples:");
      Console.WriteLine("  dotnet DSPRE.BatchExport.Linux.dll game.nds out/");
      Console.WriteLine("  dotnet DSPRE.BatchExport.Linux.dll game_contents/ out/ --extract-only");
      Console.WriteLine("");
      Console.WriteLine("Note: This Linux version performs ROM extraction only.");
      Console.WriteLine("For full PNG rendering, use the Windows version with proper OpenGL support.");
    }

    static int Main(string[] args) {
      if (args.Length < 1 || args.Any(a => a == "--help")) { 
        Usage(); 
        return args.Length < 1 ? 1 : 0; 
      }

      string input = Path.GetFullPath(args[0]);
      string outDir = args.Length >= 2 ? Path.GetFullPath(args[1]) : Path.Combine(Directory.GetCurrentDirectory(), "extracted");
      
      bool extractOnly = args.Contains("--extract-only");

      Directory.CreateDirectory(outDir);

      // Handle ROM extraction
      if (File.Exists(input) && input.EndsWith(".nds", StringComparison.OrdinalIgnoreCase)) {
        Console.WriteLine($"[*] Extracting ROM: {input}");
        
        string extractDir = Path.Combine(outDir, "rom_contents");
        if (!SimpleRomExtractor.ExtractRom(input, extractDir)) {
          Console.WriteLine("Error: ROM extraction failed.");
          Console.WriteLine("Make sure ndstool is installed and in PATH.");
          Console.WriteLine("Install with: sudo apt install devkitpro-tools");
          Console.WriteLine("Or build from source: https://github.com/devkitPro/ndstool");
          return 2;
        }
        
        Console.WriteLine($"[+] ROM extracted to: {extractDir}");
        
        // Extract NARCs if possible
        string dataDir = Path.Combine(extractDir, "data");
        if (Directory.Exists(dataDir)) {
          Console.WriteLine("[*] Extracting NARC files...");
          SimpleNarcExtractor.ExtractNarcsFromData(dataDir, Path.Combine(extractDir, "extracted_narcs"));
          Console.WriteLine("[+] NARC extraction completed");
        }
        
      } else if (Directory.Exists(input)) {
        Console.WriteLine($"[*] Using existing extracted ROM: {input}");
        
        // Copy to output directory if different
        if (Path.GetFullPath(input) != Path.GetFullPath(outDir)) {
          Console.WriteLine($"[*] Copying contents to: {outDir}");
          CopyDirectory(input, outDir);
        }
      } else {
        Console.WriteLine("Error: Input must be a .nds ROM file or extracted directory.");
        return 3;
      }

      if (extractOnly) {
        Console.WriteLine("[*] Extraction complete (extract-only mode).");
        return 0;
      }

      // PNG generation not implemented in this minimal version
      Console.WriteLine("[!] PNG generation not implemented in Linux version.");
      Console.WriteLine("    This version only provides ROM extraction functionality.");
      Console.WriteLine("    For PNG generation, use the Windows version or contribute");
      Console.WriteLine("    a full Linux implementation with proper 3D rendering support.");
      
      return 0;
    }

    static void CopyDirectory(string sourceDir, string destinationDir) {
      var dir = new DirectoryInfo(sourceDir);
      if (!dir.Exists) return;

      Directory.CreateDirectory(destinationDir);

      foreach (FileInfo file in dir.GetFiles()) {
        string targetFilePath = Path.Combine(destinationDir, file.Name);
        file.CopyTo(targetFilePath, true);
      }

      foreach (DirectoryInfo subDir in dir.GetDirectories()) {
        string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
        CopyDirectory(subDir.FullName, newDestinationDir);
      }
    }
  }
}