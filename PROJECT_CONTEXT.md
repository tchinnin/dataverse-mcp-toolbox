# Project Context: Dataverse MCP Toolbox

## Overview

The **Dataverse MCP Toolbox** is an open-source project designed to enhance Microsoft Power Platform development workflows, with a primary focus on Dataverse operations. This project provides developers with a comprehensive set of tools accessible through a standardized Model Context Protocol (MCP) interface.

## Architecture

### Core Components

1. **Local MCP Server**
   - Built using .NET and the official Dataverse Client SDK
   - Implements the Model Context Protocol specification
   - Runs locally on the developer's machine
   - Acts as a bridge between AI assistants and Dataverse operations

2. **Plugin System**
   - Extensible architecture supporting multiple .NET plugins
   - Each plugin provides specific tools/capabilities for Dataverse operations
   - Plugins register their tools with the MCP server at runtime
   - Follows a modular design allowing easy addition of new functionality

3. **VSCode Extension**
   - Provides distribution mechanism for the MCP server
   - Supports cross-platform deployment (Windows and macOS)
   - Handles end-user configuration and setup
   - Manages MCP server lifecycle (start, stop, configuration)
   - Integrates seamlessly with VSCode development environment

## Technical Stack

- **Framework**: .NET
- **SDK**: Microsoft Dataverse Client SDK
- **Protocol**: Model Context Protocol (MCP)
- **Distribution**: VSCode Extension
- **Target Platform**: Microsoft Power Platform / Dataverse

## Use Cases

This toolbox enables developers to:
- Perform Dataverse operations directly from AI-assisted development environments
- Automate common Power Platform development tasks
- Access Dataverse metadata and data programmatically
- Streamline development workflows with AI-powered tooling
- Extend capabilities through custom plugins

## Project Goals

1. **Accessibility**: Make Dataverse operations easily accessible through AI assistants
2. **Extensibility**: Provide a plugin architecture for community contributions
3. **Productivity**: Reduce friction in Power Platform development workflows
4. **Standardization**: Leverage MCP protocol for consistent tool interactions
5. **Cross-Platform**: Support Windows and macOS developers with a unified experience
6. **Open Source**: Foster community collaboration and contributions

## Key Differentiators

- **Local-First**: Runs on developer's machine with their credentials
- **Cross-Platform**: Native support for both Windows and macOS environments
- **SDK-Native**: Uses official Microsoft Dataverse Client SDK for reliable operations
- **AI-Integrated**: Designed specifically for AI assistant interactions via MCP
- **Plugin-Based**: Extensible architecture supporting custom tools
- **VSCode-Native**: Distributed and managed through familiar VSCode extension ecosystem

## Target Audience

- Power Platform developers
- Dataverse solution architects
- AI-assisted development tool users
- Microsoft ecosystem developers
- Open-source contributors interested in Power Platform tooling
