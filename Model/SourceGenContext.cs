using Watch2sftp.Core.Model;

namespace Watch2sftp.Core;


[JsonSerializable(typeof(EmailConfiguration))]
[JsonSerializable(typeof(Configuration))]
[JsonSerializable(typeof(QueueConfiguration))]
[JsonSerializable(typeof(List<MonitoredFolder>))]


[JsonSerializable(typeof(JobConfiguration))]
[JsonSerializable(typeof(List<Job>))]
[JsonSerializable(typeof(SourcePath))]
[JsonSerializable(typeof(List<DestinationPath>))]
[JsonSerializable(typeof(JobOptions))]

internal partial class AppJsonContext : JsonSerializerContext
{

}
