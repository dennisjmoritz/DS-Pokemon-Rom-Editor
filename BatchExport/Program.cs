
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using Tao.OpenGl;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using DSPRE;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using NarcAPI;

namespace DSPRE.BatchExport {
  class GLHost : GameWindow {
    public GLHost(int w, int h) : base(w, h, GraphicsMode.Default, "BatchExport", GameWindowFlags.Default, DisplayDevice.Default, 2, 1, GraphicsContextFlags.Default) {
      VSync = VSyncMode.Off;
      Visible = false; // headless
    }
  }

  static class Cam {
    public static float Ang = 0.0f;
    public static float Dist = 115.2f; // top-down like DSPRE SetCam2D
    public static float Elev = 90.0f;  // 90° = orthographic top-down
    public static float Perspective = 4.0f;
  }

  class Program {
    static void Usage() {
      Console.WriteLine("Usage:");
      Console.WriteLine("  mono DSPRE.BatchExport.exe <ROM.nds | extracted_DSPRE_contents> <out_dir> [--tile-px N] [--no-buildings] [--map-tex <id>|auto|none] [--bld-tex <id>|none]");
      Console.WriteLine("");
      Console.WriteLine("Examples:");
      Console.WriteLine("  mono DSPRE.BatchExport.exe game.nds out/ --tile-px 19 --map-tex auto");
      Console.WriteLine("  mono DSPRE.BatchExport.exe game_DSPRE_contents out/ --no-buildings --tile-px 16 --map-tex none");
    }

    static int Main(string[] args) {
      if (args.Length < 2) { Usage(); return 1; }

      string input = Path.GetFullPath(args[0]);
      string outDir = Path.GetFullPath(args[1]);
      Directory.CreateDirectory(outDir);

      int tilePx = 19; // matches DSPRE permission grid default look
      bool withBuildings = true;
      string mapTexSel = "auto"; // auto|none|<id>
      string bldTexSel = "none"; // none|<id>

      for (int i = 2; i < args.Length; i++) {
        if (args[i] == "--tile-px" && i + 1 < args.Length) { tilePx = int.Parse(args[++i]); }
        else if (args[i] == "--no-buildings") { withBuildings = false; }
        else if (args[i] == "--map-tex" && i + 1 < args.Length) { mapTexSel = args[++i]; }
        else if (args[i] == "--bld-tex" && i + 1 < args.Length) { bldTexSel = args[++i]; }
      }

      // Determine if input is ROM or extracted folder
      string workDir;
      if (File.Exists(input) && input.EndsWith(".nds", StringComparison.OrdinalIgnoreCase)) {
        // Read gameCode (4 chars) at 0xC to create RomInfo
        string gameCode;
        using (var br = new BinaryReader(File.OpenRead(input))) {
          br.BaseStream.Position = 0xC;
          gameCode = System.Text.Encoding.UTF8.GetString(br.ReadBytes(4));
        }
        var ri = new RomInfo(gameCode, input, useSuffix: true);
        if (string.IsNullOrWhiteSpace(RomInfo.romID)) {
          Console.WriteLine("Error: Unsupported ROM or failed to read header.");
          return 2;
        }
        workDir = RomInfo.workDir;

        // If not already extracted, try to extract with ndstool (Linux package: ndstool)
        if (!Directory.Exists(workDir) || Directory.GetFileSystemEntries(workDir).Length == 0) {
          Console.WriteLine("[*] Extracting ROM with ndstool -> " + workDir);
          Directory.CreateDirectory(workDir);
          var ndstool = "ndstool"; // must be in PATH
          var argsND = $"-x \"{input}\" -9 \"{RomInfo.arm9Path}\" -7 \"{Path.Combine(workDir, "arm7.bin")}\" -y9 \"{Path.Combine(workDir, "y9.bin")}\" -y7 \"{Path.Combine(workDir, "y7.bin")}\" -d \"{Path.Combine(workDir, "data")}\" -y \"{Path.Combine(workDir, "overlay")}\" -t \"{Path.Combine(workDir, "banner.bin")}\" -h \"{Path.Combine(workDir, "header.bin")}\"";
          var p = new System.Diagnostics.Process();
          p.StartInfo.FileName = ndstool;
          p.StartInfo.Arguments = argsND;
          p.StartInfo.UseShellExecute = false;
          p.StartInfo.RedirectStandardOutput = true;
          p.StartInfo.RedirectStandardError = true;
          p.Start();
          p.WaitForExit();
          if (p.ExitCode != 0) {
            Console.WriteLine(p.StandardError.ReadToEnd());
            Console.WriteLine("Error: ndstool failed. Make sure it's installed (apt install ndstool).");
            return 3;
          }
        }
      } else if (Directory.Exists(input)) {
        // Treat as already-extracted DSPRE working directory
        workDir = input.EndsWith(Path.DirectorySeparatorChar.ToString()) ? input : input + Path.DirectorySeparatorChar;
        // Best-effort: guess ROM code from header.bin
        string header = Path.Combine(workDir, "header.bin");
        string gameCode = "XXXX";
        if (File.Exists(header)) {
          using (var br = new BinaryReader(File.OpenRead(header))) {
            br.BaseStream.Position = 0xC;
            gameCode = System.Text.Encoding.UTF8.GetString(br.ReadBytes(4));
          }
        }
        var ri = new RomInfo(gameCode, Path.Combine(workDir, "FAKE.nds"), useSuffix: false);
      } else {
        Console.WriteLine("Input is neither a .nds file nor an extracted folder.");
        return 4;
      }

      // Ensure needed NARCs are unpacked (maps, textures, buildings)
      var need = new List<RomInfo.DirNames> {
        RomInfo.DirNames.maps,
        RomInfo.DirNames.mapTextures,
        RomInfo.DirNames.exteriorBuildingModels,
        RomInfo.DirNames.buildingTextures,
        RomInfo.DirNames.areaData
      };
      Console.WriteLine("[*] Unpacking required NARCs...");
      DSPRE.DSUtils.ForceUnpackNarcs(need);

      string mapsDir = RomInfo.gameDirs[RomInfo.DirNames.maps].unpackedDir;
      var mapFolders = Directory.EnumerateFileSystemEntries(mapsDir)
                                .Where(p => Directory.Exists(p))
                                .OrderBy(p => p).ToList();
      Console.WriteLine($"[*] Found {mapFolders.Count} maps.");

      // Create a tiny invisible GL surface to render into
      int w = 32 * tilePx;
      int h = 32 * tilePx;
      using (var host = new GLHost(Math.Max(64, w), Math.Max(64, h))) {
        host.MakeCurrent();
        host.Context.MakeCurrent(host.WindowInfo);
        host.Visible = false;

        // Simple GL init
        Gl.glClearColor(0.0f, 0.0f, 0.0f, 0.0f);
        Gl.glEnable(Gl.GL_DEPTH_TEST);
        Gl.glDepthFunc(Gl.GL_LEQUAL);
        Gl.glShadeModel(Gl.GL_SMOOTH);
        Gl.glEnable(Gl.GL_LIGHTING);
        Gl.glEnable(Gl.GL_LIGHT0);
        Gl.glEnable(Gl.GL_LIGHT1);
        Gl.glEnable(Gl.GL_LIGHT2);
        Gl.glEnable(Gl.GL_LIGHT3);

        var mapRenderer = new NSBMDGlRenderer();
        var bldRenderer = new NSBMDGlRenderer();

        for (int idx = 0; idx < mapFolders.Count; idx++) {
          try {
            var map = new MapFile(idx, RomInfo.gameFamily, discardMoveperms:false, showMessages:false);
            if (map.mapModel == null || map.mapModel.models == null || map.mapModel.models.Count == 0) {
              Console.WriteLine($"[-] {idx:D4}: no model, skipping");
              continue;
            }

            // Try to apply textures if requested
            if (mapTexSel != "none") {
              int texId = -1;
              if (mapTexSel == "auto") {
                // naive: try same index as map
                texId = idx;
              } else {
                int.TryParse(mapTexSel, out texId);
              }
              if (texId >= 0) {
                LoadModelTextures(map.mapModel, RomInfo.gameDirs[RomInfo.DirNames.mapTextures].unpackedDir, texId);
              }
            }

            foreach (var b in map.buildings) {
              // Load building models
              b.LoadModelData(RomInfo.GetBuildingModelsDirPath(false));
              if (withBuildings && bldTexSel != "none") {
                int tId;
                if (int.TryParse(bldTexSel, out tId)) {
                  LoadModelTextures(b.NSBMDFile, RomInfo.gameDirs[RomInfo.DirNames.buildingTextures].unpackedDir, tId);
                }
              }
            }

            // Render top-down
            RenderTopDown(ref mapRenderer, ref bldRenderer, map, w, h, withBuildings);

            using (var bmp = GrabScreenshot(w, h)) {
              string outP = Path.Combine(outDir, $"map_{idx:D4}.png");
              bmp.Save(outP, System.Drawing.Imaging.ImageFormat.Png);
              Console.WriteLine($"[+] Wrote {outP}");
            }
          } catch (Exception ex) {
            Console.WriteLine($"[!] {idx:D4} failed: {ex.Message}");
          }
        }
      }

      Console.WriteLine("[*] Done.");
      return 0;
    }

    static void LoadModelTextures(NSBMD model, string folder, int fileId) {
      if (fileId < 0) return;
      string p = Path.Combine(folder, fileId.ToString("D4"));
      if (!File.Exists(p)) return;
      try {
        model.materials = LibNDSFormats.NSBTX.NSBTXLoader.LoadNsbtx(new MemoryStream(File.ReadAllBytes(p)), out model.Textures, out model.Palettes);
        try { model.MatchTextures(); } catch {}
      } catch {}
    }

    static void ApplyBuildingTransform(Building b) {
      float fullX = b.xPosition + b.xFraction / 65536f;
      float fullY = b.yPosition + b.yFraction / 65536f;
      float fullZ = b.zPosition + b.zFraction / 65536f;

      float scaleFactor = b.NSBMDFile.models[0].modelScale / 1024f;
      float translateFactor = 256f / b.NSBMDFile.models[0].modelScale;

      Gl.glScalef(scaleFactor * b.width, scaleFactor * b.height, scaleFactor * b.length);
      Gl.glTranslatef(fullX * translateFactor / b.width, -fullY / b.height, fullZ * translateFactor / b.length);
      Gl.glRotatef(Building.U16ToDeg(b.xRotation), 1, 0, 0);
      Gl.glRotatef(Building.U16ToDeg(b.yRotation), 0, 1, 0);
      Gl.glRotatef(Building.U16ToDeg(b.zRotation), 0, 0, 1);
      Gl.glScalef(1f / b.width, 1f / b.height, 1f / b.length);
    }

    static void RenderTopDown(ref NSBMDGlRenderer mapR, ref NSBMDGlRenderer bldR, MapFile map, int w, int h, bool withBuildings) {
      // Set viewport and projection (similar to DSPRE's RenderMap for 2D)
      Gl.glViewport(0, 0, w, h);
      Gl.glMatrixMode(Gl.GL_PROJECTION);
      Gl.glLoadIdentity();
      Gl.glOrtho(-1, 1, -1, 1, 0.125, 1024);
      Gl.glMatrixMode(Gl.GL_MODELVIEW);
      Gl.glLoadIdentity();

      Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);
      Gl.glDisable(Gl.GL_BLEND);

      // Position camera
      Gl.glTranslatef(0, 0, -Cam.Dist);
      Gl.glRotatef(Cam.Elev, 1, 0, 0);
      Gl.glRotatef(Cam.Ang, 0, 1, 0);

      // Textures ON for map content
      Gl.glEnable(Gl.GL_TEXTURE_2D);

      // Render map model
      var ani = new MKDS_Course_Editor.NSBTA.NSBTA.NSBTA_File();
      var tp  = new MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File();
      var ca  = new MKDS_Course_Editor.NSBCA.NSBCA.NSBCA_File();
      int[] aniframeS = new int[0];

      mapR.Model = map.mapModel.models[0];
      mapR.RenderModel("", ani, aniframeS, aniframeS, aniframeS, aniframeS, aniframeS, ca, false, -1, 0.0f, 0.0f, Cam.Dist, Cam.Elev, Cam.Ang, true, tp, map.mapModel);

      if (withBuildings) {
        foreach (var b in map.buildings) {
          var file = b.NSBMDFile;
          if (file == null || file.models.Count == 0) continue;
          bldR.Model = file.models[0];

          Gl.glPushMatrix();
          ApplyBuildingTransform(b);
          bldR.RenderModel("", ani, aniframeS, aniframeS, aniframeS, aniframeS, aniframeS, ca, false, -1, 0.0f, 0.0f, Cam.Dist, Cam.Elev, Cam.Ang, true, tp, file);
          Gl.glPopMatrix();
        }
      }

      Gl.glFlush();
    }

    static Bitmap GrabScreenshot(int w, int h) {
      var bmp = new Bitmap(w, h);
      var data = bmp.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
      Gl.glReadPixels(0, 0, w, h, Gl.GL_BGR, Gl.GL_UNSIGNED_BYTE, data.Scan0);
      bmp.UnlockBits(data);
      bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
      return bmp;
    }
  }
}
