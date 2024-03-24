// See https://aka.ms/new-console-template for more information
using LibreHardwareMonitor.Hardware;
using Prometheus;
using System.Text.RegularExpressions;

Computer computer = new()
{
    IsCpuEnabled = true,
    IsMotherboardEnabled = true,
};

computer.Open();

Metrics.SuppressDefaultMetrics();
Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
{
    Console.WriteLine($"[{DateTimeOffset.Now}] Collecting metrics...");
    computer.Accept(new HardwareVisitor());
    Console.WriteLine($"[{DateTimeOffset.Now}] Metrics collected.");
});

Metrics.DefaultFactory
    .CreateGauge("lhm_system_boot_timestamp_seconds", "System boot timestamp in seconds")
    .Set((DateTimeOffset.Now - TimeSpan.FromMilliseconds(Environment.TickCount64)).ToUnixTimeSeconds());

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
        foreach (var subHardware in hardware.SubHardware) subHardware.Accept(this);
    }

    public void VisitSensor(ISensor sensor)
    {
        var handled = (sensor.Hardware.HardwareType, sensor.SensorType) switch
        {
            (HardwareType.Cpu, SensorType.Temperature) => this.CreateCpuTempGauge(sensor),
            (HardwareType.Cpu, SensorType.Load) => this.CreateCpuLoadGauge(sensor),
            (HardwareType.Cpu, SensorType.Power) => this.CreateCpuPowerGauge(sensor),
            (HardwareType.Cpu, SensorType.Clock) => this.CreateCpuClockGauge(sensor),
            (HardwareType.Cpu, SensorType.Voltage) => this.CreateCpuVoltageGauge(sensor),
            (HardwareType.Cpu, SensorType.Current) => this.CreateCpuCurrentGauge(sensor),
            (HardwareType.Cpu, SensorType.Factor) => this.CreateCpuFactorGauge(sensor),
            (HardwareType.SuperIO, SensorType.Voltage) => this.CreateSuperIOVoltageGauge(sensor),
            (HardwareType.SuperIO, SensorType.Control) => this.CreateSuperIOControlGauge(sensor),
            (HardwareType.SuperIO, SensorType.Temperature) => this.CreateSuperIOTemperatureGauge(sensor),
            (HardwareType.SuperIO, SensorType.Fan) => this.CreateSuperIOFanGauge(sensor),
            _ => false
        };

        if (!handled)
        {
            Console.WriteLine($"[SKIPPED SENSOR] {sensor.Hardware.HardwareType}: {sensor.Name} ({sensor.SensorType}): {sensor.Value ?? 0}");
        }
    }

    private bool CreateSuperIOFanGauge(ISensor sensor)
    {
        var id = $"{PREFIX}superio_fan_rpm";
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "SuperIO Fan").Set(sensor.Value ?? 0);
        return true;
    }

    private bool CreateSuperIOTemperatureGauge(ISensor sensor)
    {
        var id = $"{PREFIX}superio_temp_celsius";
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "SuperIO Temperature").Set(sensor.Value ?? 0);
        return true;
    }

    private bool CreateSuperIOControlGauge(ISensor sensor)
    {
        var id = $"{PREFIX}superio_control";
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "SuperIO Control").Set(sensor.Value ?? 0);
        return true;
    }

    private bool CreateSuperIOVoltageGauge(ISensor sensor)
    {
        var id = $"{PREFIX}superio_voltage_volts";
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "SuperIO Voltage").Set(sensor.Value ?? 0);
        return true;
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
    private bool CreateCpuCurrentGauge(ISensor sensor)
    {
        var id = $"{PREFIX}cpu_current_amperes";
        Metrics.DefaultFactory.WithSensorTypeLabels(sensor).CreateGauge(id, "CPU Current").Set(sensor.Value ?? 0);
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
        };

        {
            var hwname = sensor.Hardware.Name;
            var parent = sensor.Hardware.Parent;

            while (parent != null)
            {
                hwname = $"{parent.Name} › {hwname}";
                parent = parent.Parent;
            }

            labels.Add("hardware_name", hwname);
        }

        labels.Add("sensor_name", sensor.Name);

        switch (sensor.Hardware.HardwareType)
        {
            case HardwareType.Motherboard:
                break;
            case HardwareType.SuperIO:
                // labels.Add("sensor_name", sensor.Name);
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
                }
                break;
            case HardwareType.Memory:
            case HardwareType.GpuNvidia:
            case HardwareType.GpuAmd:
            case HardwareType.GpuIntel:
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
