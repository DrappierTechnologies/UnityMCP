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

## Issue Escalation and Analysis Protocol

### When Issues Arise During Task Execution

**STOP IMMEDIATELY** when encountering:
- Unexpected errors or exceptions
- Tool failures or connection issues  
- Build/compilation problems
- Test failures
- Integration conflicts
- Performance degradation
- "cb is not a function" or similar callback errors
- Port conflicts or server connection issues

### Required Response Pattern

1. **HALT EXECUTION** - Do not attempt workarounds or continue with broken functionality
2. **ENTER PLAN MODE** - Switch to analysis mode immediately
3. **ROOT CAUSE ANALYSIS** - Investigate the underlying issue systematically:
   - Read error logs and stack traces
   - Check configuration files and dependencies
   - Verify system state and prerequisites
   - Identify scope of impact (single tool vs system-wide)
   - Check for port conflicts, process conflicts, or resource locks
4. **DOCUMENT FINDINGS** - Clearly explain what went wrong and why
5. **PLAN RESOLUTION** - Present a structured fix plan using ExitPlanMode tool
6. **WAIT FOR APPROVAL** - Do not proceed until user confirms the approach

### Forbidden Behaviors During Issues

- ❌ Creating temporary scripts or workarounds
- ❌ "Let me try a different approach" without analysis
- ❌ Continuing with partial functionality
- ❌ Making assumptions about what the user wants
- ❌ Band-aid solutions that mask root problems
- ❌ Ignoring connection errors and proceeding anyway

### Required Analysis Questions

Before proposing any fix, answer:
- What is the root cause of this issue?
- What systems/components are affected?
- Is this a configuration, code, or environment problem?
- What are the potential side effects of fixing this?
- Are there upstream dependencies that need addressing?
- Could this affect other parts of the system?

### Communication Protocol

Use phrases like:
- "I've encountered an issue that requires analysis before proceeding"
- "Let me investigate the root cause and present a plan"
- "This error indicates a deeper problem that needs systematic resolution"
- "I need to stop and analyze this issue properly before continuing"

This ensures thorough problem-solving rather than hasty workarounds.

## Token Efficiency Guidelines

### Prefer Modification Over Recreation
- **ALWAYS** prefer modifying existing assets over creating new ones
- When copying prefabs/assets, modify only the specific parts that need to change
- Don't rebuild entire GameObjects - leverage existing component configurations
- Example: Instead of creating empty GameObject + adding 7 components, duplicate existing prefab and change 2 references

### Efficient Asset Workflows
- Duplicate → Rename → Modify specific parts (not rebuild from scratch)
- Preserve existing component settings, physics values, script parameters
- Only change what's actually different (sprites, controllers, specific properties)

### Tool Call Optimization  
- Batch related operations when possible
- Use project duplication instead of manual component-by-component recreation
- Leverage existing working configurations rather than starting from zero
- Think: "What's the minimal change needed?" not "How do I rebuild this?"

### Before Acting, Ask:
1. Can I modify existing asset instead of creating new?
2. What's the minimum number of changes needed?
3. Am I preserving working configurations?
4. Is there a more direct path to the goal?

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

## Unity Editor File Synchronization

**CRITICAL**: Whenever you modify ANY files in the `jp.shiranui-isuzu.unity-mcp/Editor/` folder, you MUST prompt the user to copy those files into their Unity project. The repository files are separate from the Unity project's package files, so changes won't take effect until manually copied.

**Always ask the user**: "Please copy the modified Editor files to your Unity project so the changes take effect."

## Important Notes

- Unity server runs on main thread - handlers must be thread-safe
- TypeScript handlers in `build/handlers/` are auto-loaded (no registration needed)
- C# handlers discovered via assembly scanning (internal or public access)
- Default TCP port is 27182, configurable in Unity preferences
- Connection uses localhost only for security
- DO NOT MODIFY THE BASE IMPLEMENTATION only extend
- DO NOT ATTEMPT TO REWRITE OR REFACTOR SIGNIFICANT PORTIONS OF CODE WITHOUT AUTHORIZATION
- Roll back changes you make if you've broken something that you cannot quickly fix
- Remove GameObjects that were instantiated or created in the scene for your testing purposes