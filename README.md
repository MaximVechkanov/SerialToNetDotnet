# SerialToNetDotnet
A simple serial to telnet interfacing with multi-client support implemented in C#.NET

## Features
- Multi-client support. A single device can be shared. (!) All clients receive the same echo, including remote editing, if available
- Configurable serial port baudrate, databits, stopbits (not 1.5), parity bits
- Configurable tcp port for each serial port
- Supports telnet and raw connection options
    - In raw mode no telnet-specific commands are sent or processed
- Forwards data between serial port and network byte-by-byte
- Able to cut out specified symbols from net->serial stream - configurable 
- Client identificator is asked to be used as signature
    - Prints already connected clients on connect event to see who is using the same port at the moment

## Build
1. Open `SerialToNetDotnet.sln` with Visual Studio 2019. More recent should also work.
2. Build the solution for Release/Debug

## Usage
1. Copy `SerialToNetDotnet.exe` and `YamlDotNet.dll` files from `bin/Release` ('bin/Debug') into a directory
3. Create a YAML configuration file in the same directory as used for previous point. Use `server_config.yaml` as a reference
4. Run `SerialToNetDotnet.exe` with the path to configuration file. If nothing is specified, it will use `server_config.yaml` from current directory as default.
5. If either serial or TCP port cannot be opened, corresponding interface will not be created.
6. Open a telnet connection to the host where the program is running. Use one of the ports specified in your config. This is tested only with Putty
7. You can type `status` command into the program window to print the current status of opened ports and connected clients

## TODO list
1. Use async/await instead of callbacks
2. Linux build
3. More self-functional Client (socket and send functions inside it)
4. Open serial port only on demand. Close it after last client is disconnected. Make it configurable per port (?)
5. Stability
    - Handle all errors from network and serial
    - Recover tcp servers and serial ports from erroneous states
6. Test with other telnet clients

## Acknowledgements
Inspired by ser2net. I did not find how to compile it for Windows and it does not have multiple clients support
https://github.com/UngarMax/TelnetServer - used as reference for telnet server implementation
