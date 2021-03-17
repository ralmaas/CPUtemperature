# CPUtemperature
A small Windows Service program used for reporting the CPU temperature using MQTT at regular intervals.

Two NuGet packages used: M2Mqtt(4.3.0.0) and OpenHardwareMonitorLib (0.7.1.0).

Since the Service is being run on a Laptop that is used on two different networks I have added a list of MQTT-hosts. If connection fails the program will test the next entry in the list... 

## Testing
This code has been verified on both Intel and AMD CPU.

> 'roar@nsi:~$ mosquitto_sub -h localhost -t Asus/# -v'
> 'Asus/Temp/CPU 51'
> 'Asus/Temp/CPU 53'
