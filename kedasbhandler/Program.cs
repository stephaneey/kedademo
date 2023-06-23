using Azure.Messaging.ServiceBus;
bool RunAsJob = false;
string ConnectionString = Environment.GetEnvironmentVariable("TargetBus");
string TargetTopic = Environment.GetEnvironmentVariable("TargetTopic");
string TargetSubscription = Environment.GetEnvironmentVariable("TargetSubscription");
RunAsJob = (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RunAsJob"))) ? Convert.ToBoolean(Environment.GetEnvironmentVariable("RunAsJob")) : false;
ManualResetEvent resetEvent = new ManualResetEvent(false);
#if DEBUG
//local testing values
#endif
if (string.IsNullOrEmpty(ConnectionString) || string.IsNullOrEmpty(TargetTopic) || string.IsNullOrEmpty(TargetSubscription))
{
    throw new ApplicationException("TargetBus, TargetTopic and TargetSubscription are not supplied or are wrong");
}
try
{

    
    ServiceBusClient client = new ServiceBusClient(ConnectionString);
    ServiceBusProcessor processor = client.CreateProcessor(TargetTopic, TargetSubscription, new ServiceBusProcessorOptions());
    processor.ProcessMessageAsync += MessageHandler;
    processor.ProcessErrorAsync += ErrorHandler;
    await processor.StartProcessingAsync();
    Console.WriteLine("Waiting for messages");
    resetEvent.WaitOne();
    await processor.StopProcessingAsync();


}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
    throw;
}

async Task MessageHandler(ProcessMessageEventArgs args)
{
    string body = args.Message.Body.ToString();
    Console.WriteLine($"Received: {body} from subscription.");
    // complete the message. messages is deleted from the subscription. 
    await args.CompleteMessageAsync(args.Message);
    //a job must terminate to be seen as completed by K8s. I just process a single message and then I stop
    //if not running as a job, the program will wait for a sig kill to terminate
    if (RunAsJob) resetEvent.Set();
}

// handle any errors when receiving messages
Task ErrorHandler(ProcessErrorEventArgs args)
{
    Console.WriteLine(args.Exception.ToString());
    return Task.CompletedTask;
}