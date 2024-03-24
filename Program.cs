// See https://aka.ms/new-console-template for more information
using System.Text.RegularExpressions;
using LibreHardwareMonitor.Hardware;
using Prometheus;

Computer computer = new Computer
{
    IsCpuEnabled = true,
    IsGpuEnabled = true,
    IsMemoryEnabled = true,
    IsMotherboardEnabled = true,
    IsControllerEnabled = true,
    // IsNetworkEnabled = false,
    // IsStorageEnabled = true
};

computer.Open();


Metrics.SuppressDefaultMetrics();
Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
{
    Console.WriteLine("Collecting metrics...");
    computer.Accept(new HardwareVisitor());
});

computer.Accept(new HardwareVisitor());

var metricServer = new KestrelMetricServer(port: 6272);
metricServer.Start();

Console.WriteLine("Press Ctrl+C to exit.");

// Wait until Ctrl+C is pressed
ManualResetEvent _quitEvent = new(false);
Console.CancelKeyPress += (sender, eArgs) => { _quitEvent.Set(); eArgs.Cancel = true; };
_quitEvent.WaitOne();

//
metricServer.Stop();
computer.Close();

public partial class HardwareVisitor : IVisitor
{
    private const string PREFIX = "lhm_";

    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        hardware.Traverse(this);
        foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
    }

    public void VisitSensor(ISensor sensor)
    {
        // var id = $"lhm_{sensor.Hardware.HardwareType.ToString().ToLower()}_{sensor.Identifier.ToString().Replace('/', '_').TrimStart('_')}";
        var id = "lhm_" + sensor.Identifier.ToString().Replace('/', '_').Replace("-", null).TrimStart('_');
        // Console.WriteLine($"{sensor.Name} ({sensor.SensorType}): {sensor.Value ?? 0}");
        // Metrics.DefaultFactory.CreateGauge(id, $"{sensor.Hardware.HardwareType}: {sensor.Name} ({sensor.SensorType})").Set(sensor.Value ?? 0);

        switch (sensor.Hardware.HardwareType)
        {
            case HardwareType.Motherboard:
                break;
            case HardwareType.SuperIO:
                break;
            case HardwareType.Cpu:
                break;
            case HardwareType.Memory:
                break;
            case HardwareType.GpuNvidia:
                break;
            case HardwareType.GpuAmd:
                break;
            case HardwareType.GpuIntel:
                break;
            case HardwareType.Storage:
                break;
            case HardwareType.Network:
                break;
            case HardwareType.Cooler:
                break;
            case HardwareType.EmbeddedController:
                break;
            case HardwareType.Psu:
                break;
            case HardwareType.Battery:
                break;
            default:
                break;
        }

        var handled = (sensor.Hardware.HardwareType, sensor.SensorType) switch
        {
            (HardwareType.Cpu, SensorType.Temperature) => CreateCpuTempGauge(sensor),
            (HardwareType.Cpu, SensorType.Load) => CreateCpuLoadGauge(sensor),
            (HardwareType.Cpu, SensorType.Power) => CreateCpuPowerGauge(sensor),
            (HardwareType.Cpu, SensorType.Clock) => CreateCpuClockGauge(sensor),
            (HardwareType.Cpu, SensorType.Voltage) => CreateCpuVoltageGauge(sensor),
            (HardwareType.Cpu, SensorType.Factor) => CreateCpuFactorGauge(sensor),
            _ => false
        };

        if (!handled)
        {
            Console.WriteLine($"[SKIPPED SENSOR] {sensor.Hardware.HardwareType}: {sensor.Name} ({sensor.SensorType}): {sensor.Value ?? 0}");
        }
    }

    private bool CreateCpuTempGauge(ISensor sensor)
    {
        var id = $"{PREFIX}cpu_temp_celsius";
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "CPU Temperature").Set(sensor.Value ?? 0);
        return true;
    }

    private bool CreateCpuLoadGauge(ISensor sensor)
    {
        var id = $"{PREFIX}cpu_load_ratio";
        // 从 0-100 转换为 0-1
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "CPU Load").Set((sensor.Value ?? 0) / 100);
        return true;
    }

    private bool CreateCpuPowerGauge(ISensor sensor)
    {
        var id = $"{PREFIX}cpu_power_watts";
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "CPU Power").Set(sensor.Value ?? 0);
        return true;
    }

    private bool CreateCpuClockGauge(ISensor sensor)
    {
        var id = $"{PREFIX}cpu_clock_mhz";
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "CPU Clock").Set(sensor.Value ?? 0);
        return true;
    }
    private bool CreateCpuVoltageGauge(ISensor sensor)
    {
        var id = $"{PREFIX}cpu_voltage_volts";
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "CPU Voltage").Set(sensor.Value ?? 0);
        return true;
    }
    private bool CreateCpuFactorGauge(ISensor sensor)
    {
        var id = $"{PREFIX}cpu_factor";
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "CPU Factor").Set(sensor.Value ?? 0);
        return true;
    }

    public void VisitParameter(IParameter parameter) { }
}

public static partial class MetricFactoryExtensions
{
    [GeneratedRegex(@"Core #(\d+)", RegexOptions.Compiled)]
    private static partial Regex GetCpuCoreRegex();

    public static IMetricFactory WithSensorTypeLabels(this IMetricFactory factory, ISensor sensor)
    {
        Dictionary<string, string> labels = new()
        {
            { "sensor_type",sensor.SensorType.ToString() },
            { "hardware_type", sensor.Hardware.HardwareType.ToString() },
            { "hardware_name", sensor.Hardware.Name },
        };

        switch (sensor.Hardware.HardwareType)
        {
            case HardwareType.Motherboard:
                break;
            case HardwareType.SuperIO:
                break;
            case HardwareType.Cpu:
                var match = GetCpuCoreRegex().Match(sensor.Name);
                if (match.Success)
                {
                    labels.Add("cpu", "core");
                    labels.Add("cpu_core", match.Groups[1].Value);
                }
                else
                {
                    labels.Add("cpu", "package");
                    labels.Add("sensor_name", sensor.Name);
                }
                break;
            case HardwareType.Memory:
                break;
            case HardwareType.GpuNvidia:
                break;
            case HardwareType.GpuAmd:
                break;
            case HardwareType.GpuIntel:
                break;
            case HardwareType.Storage:
            case HardwareType.Network:
            case HardwareType.Cooler:
            case HardwareType.EmbeddedController:
            case HardwareType.Psu:
            case HardwareType.Battery:
            default:
                break;
        }

        return factory.WithLabels(labels);
    }
}
