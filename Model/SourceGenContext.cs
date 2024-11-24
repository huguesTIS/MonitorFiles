
namespace Watch2sftp.Core;


[JsonSerializable(typeof(JobConfiguration))]
[JsonSerializable(typeof(List<Job>))]
[JsonSerializable(typeof(SourcePath))]
[JsonSerializable(typeof(List<DestinationPath>))]
[JsonSerializable(typeof(JobOptions))]

internal partial class AppJsonContext : JsonSerializerContext
{

}
