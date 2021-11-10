# SerialToNetDotnet
A simple serial to telnet interfacing with multi-client support implemented in C#.NET

## Features
- Multi-client support. A single device can be shared. (!) All clients receive the same echo, including remote editing, if available
- Configurable serial port baudrate, databits, stopbits (not 1.5), parity bits
- Configurable tcp port for each serial port
- Supports only telnet as network protocol
- Forwards data between serial port and network byte-by-byte
- Client identificator is asked to be used as signature
- Prints already connected clients on connect to see who is using the same port at the moment

## Usage
1. Open `SerialToNetDotnet.sln` with Visual Studio 2019. More recent should also work. Build the solution for Release
2. Copy `SerialToNetDotnet.exe` and `YamlDotNet.dll` files from `bin/Release` into a directory
3. Create a YAML configuration file in the same directory as used for previous point. Use `server_config.yaml` as a reference
4. Run `SerialToNetDotnet.exe` with the path to configuration file. If nothing is specified, it will use `server_config.yaml` from current directory as default.
5. If either serial or TCP port cannot be opened, corresponding interface will not be created.
6. Open a telnet connection to the host with program running and one of the ports specified in your config. This is tested only with Putty
7. Enjoy
8. You can type `status` command into the program window to print the current status of opened ports and connected clients

## TODO List
- Linux build
- Add configurable network protocol to use (RAW is the next one)
- Open serial port only on demand. Close it after last client is disconnected
- Use async/await instead of callbacks (?)

## Acknowledgements
Inspired by ser2net. I did not find how to compile it for Windows and it does not have multiple clients support
https://github.com/UngarMax/TelnetServer - used as reference for telnet server implementation
