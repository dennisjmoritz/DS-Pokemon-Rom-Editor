# DS Pokemon ROM Editor - Linux Batch Export Tool

This is a Linux-compatible version of the DS Pokemon ROM Editor's batch export functionality. This version provides ROM extraction capabilities and is designed to run on Linux systems without Windows-specific dependencies.

## Features

- Cross-platform ROM extraction using ndstool
- Simplified command-line interface
- No Windows dependencies
- Built with .NET 8.0 for modern Linux compatibility

## Prerequisites

### Required Tools

1. **.NET 8.0 Runtime** - Already installed on most modern Linux distributions
   ```bash
   # Ubuntu/Debian
   sudo apt update
   sudo apt install dotnet-runtime-8.0
   
   # Or check if already installed
   dotnet --version
   ```

2. **ndstool** - Nintendo DS ROM manipulation tool
   ```bash
   # Install from devkitPro
   sudo apt install devkitpro-tools
   
   # Or build from source
   git clone https://github.com/devkitPro/ndstool.git
   cd ndstool
   make
   sudo make install
   ```

## Building

```bash
cd BatchExport.Linux
dotnet build
```

## Usage

### Extract ROM Only
```bash
dotnet run -- pokemon.nds extracted_output/ --extract-only
```

### Basic Usage
```bash
# Extract ROM (PNG generation not implemented yet)
dotnet run -- pokemon.nds output_directory/

# Show help
dotnet run -- --help
```

## Current Limitations

This Linux version currently only implements ROM extraction functionality. The full PNG rendering pipeline from the original Windows version is not yet implemented due to the complexity of porting the 3D rendering and NSBMD model loading systems.

### What Works
- ✅ ROM extraction with ndstool
- ✅ Basic file system operations
- ✅ Cross-platform directory handling

### What's Missing (Future Work)
- ❌ 3D model rendering (NSBMD files)
- ❌ PNG map generation
- ❌ Texture loading and application
- ❌ Building model rendering

## Contributing

To implement full PNG generation support, the following would need to be ported:

1. **NSBMD Model Loading** - Convert LibNDSFormats to work with modern .NET
2. **OpenGL Rendering** - Replace Tao.OpenGL with modern OpenTK
3. **NARC File Handling** - Port NARC extraction from the main project
4. **Texture Management** - Implement cross-platform texture loading

## Original Windows Version

For full functionality including PNG generation, use the original Windows version with the complete DSPRE toolkit.

## License

This project maintains the same license as the parent DS-Pokemon-Rom-Editor project.