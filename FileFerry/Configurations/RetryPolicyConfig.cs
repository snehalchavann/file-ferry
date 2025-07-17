namespace FileFerry.Configurations;

public class RetryPolicyConfig
{
    public int MaxRetries { get; set; }
    public int DelayMilliseconds { get; set; }
}