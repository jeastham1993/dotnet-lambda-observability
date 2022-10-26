namespace ObservableLambda.Test.AwsOddTestFramework;

public class LambdaInvokeProps
{
    public LambdaInvokeProps(string handler, params object[] constructorParameters)
    {
        this.Handler = handler;
        this.ConstructorParameters = constructorParameters;
    }
    
    public string Handler { get; set; }
    
    public object[] ConstructorParameters { get; set; }
}