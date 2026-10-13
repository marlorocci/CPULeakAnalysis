# CPULeakAnalysis

WPF/MVVM utility that records CPU and working-set memory for Milestone/XProtect processes only.

## Build
1. Install the .NET 8 SDK and Visual Studio 2022 with **.NET desktop development**.
2. Open `CPULeakAnalysis.sln` or run `dotnet restore` then `dotnet build`.
3. Run elevated if process metadata for services cannot be read under the current account.

## Detection
A process is included when its process name, executable path, CompanyName, ProductName, or FileDescription contains `Milestone`, `VideoOS`, or `XProtect`. Identity results are cached per PID.

## Storage
SQLite file: `%LOCALAPPDATA%\CPULeakAnalysis\cpu-history.db`

The grid loads the last 24 hours. The chart displays the last 30 minutes. **Clear database** asks for confirmation, deletes all rows, and compacts the database.

## CPU calculation
CPU is calculated from the delta in `Process.TotalProcessorTime`, divided by wall-clock time and `Environment.ProcessorCount`. Therefore 100% means all logical processors are saturated by that process.
