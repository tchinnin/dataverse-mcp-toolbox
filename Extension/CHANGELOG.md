# Change Log

All notable changes to the Dataverse MCP ToolBox extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.260204] - 2026-02-04

### Added

- 🎉 **Initial Public Release**
- Automatic MCP server download and installation from NuGet
- Multi-platform support (macOS Intel/ARM, Windows, Linux)
- Connection management with OAuth authentication
- Visual interface in Activity Bar for managing connections
- Plugin system with NuGet-based plugin installation
- GitHub Copilot integration through Model Context Protocol
- MCP configuration auto-registration
- Server lifecycle management (start, stop, restart)
- WhoAmI plugin bundled for quick testing
- Server channel selection (stable vs prerelease)
- Version enforcement for locked deployments

### Features

#### Connection Management
- Add/remove Dataverse connections
- OAuth authentication flow
- Multiple environment support
- Active connection selection
- Connection status indicators

#### Plugin System
- Install plugins from NuGet packages
- View installed plugins and available tools
- Uninstall plugins
- Tool execution with custom parameters
- Plugin discovery and loading

#### MCP Integration
- Automatic server registration in mcp.json
- Bridge for GitHub Copilot communication
- JSON-RPC protocol implementation
- Tool schema generation and validation

#### Developer Experience
- Comprehensive logging in Output panel
- Server info view with version and status
- Connection testing and validation
- Error messages and troubleshooting hints

### Technical Details

- **Runtime**: DataverseMCPToolBox.Runtime v0.2.260204
- **Extensibility SDK**: DataverseMCPToolBox.Extensibility v0.2.260204
- **MCP Protocol**: JSON-RPC 2.0 over named pipes (Windows) and Unix domain sockets (macOS/Linux)
- **Authentication**: MSAL (Microsoft Authentication Library) for OAuth flows
- **Plugin Loading**: Dynamic assembly loading with isolated contexts

### Known Issues

- First connection may take a few seconds while acquiring OAuth token
- Plugin installation requires restart of MCP server to take effect
- Large tool responses may cause delays in Copilot UI

### Platform Support

- ✅ macOS 11+ (Intel x64 and Apple Silicon ARM64)
- ✅ Windows 10/11 (x64)
- ✅ Linux (x64, tested on Ubuntu 20.04+)

---

## [Unreleased]

### Planned Features

- Tool execution history and replay
- Connection credential storage improvements
- Plugin dependency resolution
- Batch tool execution
- Connection import/export
- Enhanced error recovery
- Performance optimizations

---

## About Version Numbers

Version numbers follow the format: `MAJOR.MINOR.YYMMDD`

- **MAJOR**: Breaking changes to architecture or APIs
- **MINOR**: New features or significant enhancements
- **YYMMDD**: Build date (Year-Month-Day)

Example: `0.2.260204` = Version 0.2, built on February 4th, 2026
