# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

Unity MCP (Model Context Protocol) integration framework that enables AI language models like Claude to interact directly with the Unity Editor through an extensible handler architecture. The project consists of:

1. **Unity C# Plugin** (`jp.shiranui-isuzu.unity-mcp/`) - Unity package providing the server and handler infrastructure
2. **TypeScript MCP Client** (`unity-mcp-ts/`) - Node.js client that bridges AI services with Unity

## Build Commands

### TypeScript Client (unity-mcp-ts/)
```bash
cd unity-mcp-ts
npm install        # Install dependencies
npm run build      # Build TypeScript to JavaScript
npm run dev        # Run in development mode with tsx
npm run test       # Run tests with vitest
npm run lint       # Lint TypeScript files
npm run lint:fix   # Auto-fix linting issues
```

### Unity Package
The Unity package is installed via Unity Package Manager. No build commands needed - Unity compiles C# automatically.

## Architecture

### Handler System
The framework uses a handler-based architecture with automatic discovery:

- **Command Handlers** (Tools): Execute actions in Unity, implement `IMcpCommandHandler` (C#) or extend `BaseCommandHandler` (TypeScript)
- **Resource Handlers**: Provide data access, implement `IMcpResourceHandler` (C#) or extend `BaseResourceHandler` (TypeScript)  
- **Prompt Handlers**: Template-based workflows, extend `BasePromptHandler` (TypeScript only)

### Key Components

**Unity Side (C#)**:
- `McpServer`: TCP server managing connections and routing (port 27182 default)
- `McpHandlerDiscovery`: Auto-discovers handlers via assembly scanning
- `McpServiceManager`: Dependency injection for handler services
- Handler interfaces in `jp.shiranui-isuzu.unity-mcp/Editor/Core/`

**TypeScript Side**:
- `UnityConnection`: TCP client connection management
- `HandlerAdapter`: Adapts handlers to MCP SDK format
- `HandlerDiscovery`: Auto-loads handlers from build/handlers/
- Base handler classes in `unity-mcp-ts/src/core/`

### Communication Flow
1. AI invokes MCP function → TypeScript client receives
2. Client forwards to Unity via TCP (localhost:27182)
3. Unity McpServer routes to appropriate handler
4. Handler processes on Unity main thread
5. Response flows back through TCP to AI

## Development Guidelines

### Creating Custom Handlers

**C# Command Handler**:
- Implement `IMcpCommandHandler` interface
- Place anywhere in Unity project - auto-discovered
- Use `CommandPrefix` for unique command namespace
- Return JObject with success/error status

**TypeScript Handler**:
- Extend appropriate base class (BaseCommandHandler, BaseResourceHandler, BasePromptHandler)
- Place compiled .js file in `build/handlers/` directory
- Define tool/resource definitions with Zod schemas
- Forward Unity requests via `sendUnityRequest()`

### Handler Naming Convention
- Command prefix should be lowercase, descriptive (e.g., "hierarchy", "console")
- Actions use dot notation: `prefix.action` (e.g., "hierarchy.get", "console.clear")

### Patterns & Principles
- Respect the Single Responsibility principle
- Respect the Open-Closed principle
- Respect the Liskov Substitution principle
- Respect the Interface Segregation principle
- Respect the Dependency Inversion principle
- Adhere to Clean Code and Clean Architecture software design
- Adhere to this projects architecture as much as possible

## Testing

### TypeScript Tests
```bash
cd unity-mcp-ts
npm run test
```

### Unity Testing
Test handlers directly in Unity Editor:
1. Open Unity project with package installed
2. Edit > Preferences > Unity MCP
3. Click "Connect" to start server
4. Test handlers via Claude Desktop or MCP Inspector

## Important Notes

- Unity server runs on main thread - handlers must be thread-safe
- TypeScript handlers in `build/handlers/` are auto-loaded (no registration needed)
- C# handlers discovered via assembly scanning (internal or public access)
- Default TCP port is 27182, configurable in Unity preferences
- Connection uses localhost only for security
- DO NOT MODIFY THE BASE IMPLEMENTATION only extend
- DO NOT ATTEMPT TO REWRITE OR REFACTOR SIGNIFICANT PORTIONS OF CODE WITHOUT AUTHORIZATION
- Roll back changes you make if you've broken something that you cannot quickly fix