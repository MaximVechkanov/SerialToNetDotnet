# SerialToNetDotnet
A simple serial to telnet interfacing with multi-client support implemented in C#.NET

## Features
- Multi-client support. A single device can be shared. (!) All clients receive the same echo, including remote editing, if available
- Configurable serial port baudrate
- Configurable tcp port for each serial port
- Supports only telnet as network protocol
- Forwards data between serial port and network byte-by-byte
- Client identificator to use as signature
- Prints already connected clients on connect to see who is using the same port at the moment

## TODO
- Add configurable network protocol to use (RAW is the next one)
- Open serial port only on demand. Close it after last client is disconnected

## Acknowledgements
Inspired by ser2net. I did not find how to compile it for Windows and it does not have multiple clients support
https://github.com/UngarMax/TelnetServer - used as reference for telnet server implementation
