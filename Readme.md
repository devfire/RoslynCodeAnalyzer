# Roslyn Code Analyzer

## Overview
The Roslyn Code Analyzer is a static code analysis tool built on the Roslyn platform. It inspects source code and provides insights into code structure and data models. This tool can help developers understand and improve code quality through detailed analysis.

## Features
- **Code Analysis:** Uses Roslyn to traverse and analyze code files.
- **Detailed Structural Insights:** Provides information on code organization and component relationships.
- **Extensibility:** Easily extendable with custom analyzers.
- **Command Line Interface:** Run analysis directly from the terminal.

## Project Structure
```
.
├── .gitignore
├── CodeStructureWalker.cs
├── DataModel.cs
├── Program.cs
├── RoslynCodeAnalyzer.csproj
├── bin/
│   ├── Debug/
│   │   └── net8.0/
│   └── Release/
│       └── net8.0/
└── obj/
    ├── ...existing build files...
```

## Requirements
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A code editor like [Visual Studio Code](https://code.visualstudio.com/)

## Installation & Setup
1. **Clone the Repository:**
   ```sh
   git clone https://github.com/your-username/RoslynCodeAnalyzer.git
   cd RoslynCodeAnalyzer
   ```
2. **Restore Dependencies:**
   ```sh
   dotnet restore
   ```

## Building the Project
Build the project using the .NET CLI:
```sh
dotnet build
```
This command compiles the project and outputs binaries to the `bin/` directory based on the configuration (Debug/Release).

## Running the Application
After building, you can run the application with:
```sh
dotnet run
```
The analysis results will be printed to the console.

## Analysis Process
1. **Entry Point (`Program.cs`):** Orchestrates the application workflow.
2. **Code Traversal:** `CodeStructureWalker.cs` navigates through the source files.
3. **Data Models:** `DataModel.cs` defines the structures used to represent analyzed code.
4. **Execution Flow:** 
   - The application starts in `Program.cs`.
   - It initializes the analyzer.
   - The analyzer processes the code using Roslyn’s APIs.
   - Analysis output is generated and displayed in the terminal.

## Contributing
Contributions are welcome! To contribute:
1. Fork the repository.
2. Create a feature branch:
   ```sh
   git checkout -b feature/my-feature
   ```
3. Commit your changes:
   ```sh
   git commit -am 'Add new feature'
   ```
4. Push to your branch:
   ```sh
   git push origin feature/my-feature
   ```
5. Open a Pull Request describing your changes.

Please adhere to the project’s coding standards and include necessary tests for your contributions.

## Troubleshooting / FAQ
- **Q: I built the project but cannot run it. What should I do?**  
  **A:** Ensure that the .NET 8.0 SDK is installed and that all dependencies have been restored with `dotnet restore`.

- **Q: How do I add a custom analyzer?**  
  **A:** Extend the logic in `CodeStructureWalker.cs` and update `DataModel.cs` as needed. Contributions that add robust custom analyzers are welcome.

- **Q: Where can I see the analysis output?**  
  **A:** The output is displayed in the terminal after running the application with `dotnet run`.

## License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.