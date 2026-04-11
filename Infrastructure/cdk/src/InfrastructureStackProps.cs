using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;

namespace Infrastructure
{
    public class InfrastructureStackProps: StackProps
    {
        public IVpc Vpc { get; set; }
    }
}