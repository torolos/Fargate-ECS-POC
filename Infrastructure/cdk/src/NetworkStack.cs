using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Constructs;

namespace Infrastructure
{
    public class NetworkStack : Stack
    {
        public IVpc Vpc { get; }

        public NetworkStack(Construct scope, string id, IStackProps props = null)
            : base(scope, id, props)
        {
            Vpc = new Vpc(this, "FargateEcsPocVpc", new VpcProps
            {
                IpAddresses = IpAddresses.Cidr("10.0.0.0/16"),
                MaxAzs = 2,
                NatGateways = 1
            });

            new CfnOutput(this, "VpcId", new CfnOutputProps
            {
                Value = Vpc.VpcId,
                ExportName = "SharedVpcId"
            });
        }
    }
}