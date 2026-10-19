namespace MaterialOverhaul.Power
{
    /// <summary>
    /// 设计公式的不可变参数。没有正式默认值，调用方必须明确提供。
    /// 非负指数与系数保证损耗及总支出随交付功率单调不减。
    /// </summary>
    public sealed class LineLossParameters
    {
        public double Pc { get; }
        public double L0 { get; }
        public double PowerReference { get; }
        public double ConductivityReference { get; }
        public double A0 { get; }
        public double A1 { get; }
        public double Nu { get; }
        public double Gamma { get; }
        public double Alpha { get; }
        public double Beta { get; }
        public double LambdaMin { get; }
        public double DeltaLambda { get; }
        public double K { get; }
        public double A { get; }

        public LineLossParameters(double pc, double l0, double powerReference, double conductivityReference,
            double a0, double a1, double nu, double gamma, double alpha, double beta,
            double lambdaMin, double deltaLambda, double k, double a)
        {
            PowerNumbers.Positive(pc, nameof(pc));
            PowerNumbers.Positive(l0, nameof(l0));
            PowerNumbers.Positive(powerReference, nameof(powerReference));
            PowerNumbers.Positive(conductivityReference, nameof(conductivityReference));
            PowerNumbers.Nonnegative(a0, nameof(a0));
            PowerNumbers.Nonnegative(a1, nameof(a1));
            PowerNumbers.Nonnegative(nu, nameof(nu));
            PowerNumbers.Nonnegative(gamma, nameof(gamma));
            PowerNumbers.Nonnegative(alpha, nameof(alpha));
            PowerNumbers.Nonnegative(beta, nameof(beta));
            PowerNumbers.Nonnegative(lambdaMin, nameof(lambdaMin));
            PowerNumbers.Nonnegative(deltaLambda, nameof(deltaLambda));
            PowerNumbers.Nonnegative(k, nameof(k));
            PowerNumbers.Nonnegative(a, nameof(a));
            Pc = pc; L0 = l0; PowerReference = powerReference; ConductivityReference = conductivityReference;
            A0 = a0; A1 = a1; Nu = nu; Gamma = gamma; Alpha = alpha; Beta = beta;
            LambdaMin = lambdaMin; DeltaLambda = deltaLambda; K = k; A = a;
        }
    }
}
