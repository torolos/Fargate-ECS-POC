using Amazon.CDK;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.IAM;
using Constructs;
using System.Collections.Generic;

namespace Infrastructure
{
    public class InfrastructureStack : Stack
    {
        internal InfrastructureStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            Amazon.CDK.Tags.Of(this).Add("CreatedBy", "dtorolopoulos");
            Amazon.CDK.Tags.Of(this).Add("Purpose", "POC");

            var githubOidcProvider = new OpenIdConnectProvider(
                this, "GitHubOIDCProvider", 
                new OpenIdConnectProviderProps
                {
                    Url = "https://token.actions.githubusercontent.com",
                    ClientIds = ["sts.amazonaws.com"]
                }
            );

            var githubRole = new Role(this, "GitHubActionsRole", new RoleProps
            {
                RoleName = "fargate-ecs-poc-github-actions-role",
                AssumedBy = new FederatedPrincipal(
                    githubOidcProvider.OpenIdConnectProviderArn,
                    new Dictionary<string, object>
                    {
                        ["StringEquals"] = new Dictionary<string, string>
                        {
                            ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com",
                            // ["token.actions.githubusercontent.com:sub"] = "repo:dimitris-torolopoulos/Fargate-ECS-POC:ref:refs/heads/main"
                        },
                        ["StringLike"] = new Dictionary<string, string>
                        {
                            // ["token.actions.githubusercontent.com:sub"] = "repo:dimitris-torolopoulos/Fargate-ECS-POC:*"
                            ["token.actions.githubusercontent.com:sub"] = "repo:*:*"
                        }
                    },
                    "sts:AssumeRoleWithWebIdentity"
                ),
                Description = "Role for GitHub Actions to deploy to ECS Fargate",
            });

            // githubRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AdministratorAccess"));
            githubRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] 
                {
                    "ecr:GetAuthorizationToken",
                    "ecr:BatchCheckLayerAvailability",
                    "ecr:GetDownloadUrlForLayer",
                    "ecr:BatchGetImage",
                    "ecr:PutImage",
                    "ecr:InitiateLayerUpload",
                    "ecr:UploadLayerPart",
                    "ecr:CompleteLayerUpload",
                    "ecs:UpdateService",
                    "ecs:DescribeServices"
                },
                Resources = new[] { "*" }
            }));

            var dotnetImageTag = Node.TryGetContext("dotnetImageTag")?.ToString() ?? "latest";
            var nodeImageTag = Node.TryGetContext("nodeImageTag")?.ToString() ?? "latest";

            var vpc = new Vpc(this, "FargateEcsPocVpc", new VpcProps
            {
                IpAddresses = IpAddresses.Cidr("10.0.0.0/16"),
                MaxAzs = 2,
                NatGateways = 1,
            });

            var cluster = new Cluster(this, "FargateEcsPocCluster", new ClusterProps
            {
                ClusterName = "fargate-ecs-poc-cluster",
                Vpc = vpc
            });
            
            var dotnetRepository = new Repository(this, "DotnetAppRepository", new RepositoryProps
            {
                RepositoryName = "fargate-ecs-poc-dotnet-app-repo",
                ImageScanOnPush = true,
                RemovalPolicy = RemovalPolicy.DESTROY,
                LifecycleRules = [
                    new LifecycleRule
                    {
                        MaxImageCount = 10,
                        RulePriority = 1,
                        Description = "Keep only the latest 10 images"
                    }
                ]
            });

            var nodeRepository = new Repository(this, "NodeAppRepository", new RepositoryProps
            {
                RepositoryName = "fargate-ecs-poc-node-app-repo",
                ImageScanOnPush = true,
                RemovalPolicy = RemovalPolicy.DESTROY,
                LifecycleRules = [
                    new LifecycleRule
                    {
                        MaxImageCount = 10,
                        RulePriority = 1,
                        Description = "Keep only the latest 10 images"
                    }
                ]
            });

            var dotnetLogGroup = new Amazon.CDK.AWS.Logs.LogGroup(this, "DotnetApiLogGroup", new Amazon.CDK.AWS.Logs.LogGroupProps
            {
                LogGroupName = "ecs/fargate-ecs-poc/dotnet-api",
                Retention = Amazon.CDK.AWS.Logs.RetentionDays.ONE_DAY,
                RemovalPolicy = RemovalPolicy.SNAPSHOT
            });

            var nodeLogGroup = new Amazon.CDK.AWS.Logs.LogGroup(this, "NodeApiLogGroup", new Amazon.CDK.AWS.Logs.LogGroupProps
            {
                LogGroupName = "ecs/fargate-ecs-poc/node-api",
                Retention = Amazon.CDK.AWS.Logs.RetentionDays.ONE_DAY,
                RemovalPolicy = RemovalPolicy.SNAPSHOT
            });

            var dotnetTaskDefinition = new FargateTaskDefinition(this, "DotnetApiTaskDef", new FargateTaskDefinitionProps
            {
                Cpu = 256,
                MemoryLimitMiB = 512,
                Family = "fargate-ecs-poc-dotnet-api-task-def",
            });

            var dotnetContainer = dotnetTaskDefinition.AddContainer("DotnetApiContainer", new Amazon.CDK.AWS.ECS.ContainerDefinitionOptions
            {
                ContainerName = "dotnet-api",
                Image = ContainerImage.FromEcrRepository(dotnetRepository, dotnetImageTag),
                Logging = LogDrivers.AwsLogs(new AwsLogDriverProps
                {
                    LogGroup = dotnetLogGroup,
                    StreamPrefix = "dotnet-api"
                }),
                Environment = new Dictionary<string, string>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                    ["ASPNETCORE_URLS"] = "http://+:80"
                }   
            });
            dotnetContainer.AddPortMappings(new PortMapping
            {
                ContainerPort = 8080
            });
            dotnetRepository.GrantPullPush(dotnetTaskDefinition.ExecutionRole);

            var nodeTaskDefinition = new FargateTaskDefinition(this, "NodeApiTaskDef", new FargateTaskDefinitionProps
            {
                Cpu = 256,
                MemoryLimitMiB = 512,
                Family = "fargate-ecs-poc-node-api-task-def",
            });

            var nodeContainer = nodeTaskDefinition.AddContainer("NodeApiContainer", new Amazon.CDK.AWS.ECS.ContainerDefinitionOptions
            {
                ContainerName = "node-api",
                Image = ContainerImage.FromEcrRepository(nodeRepository, nodeImageTag),
                Logging = LogDrivers.AwsLogs(new AwsLogDriverProps
                {
                    LogGroup = nodeLogGroup,
                    StreamPrefix = "node-api"
                }),
                Environment = new Dictionary<string, string>
                {
                    ["NODE_ENV"] = "development",
                    ["PORT"] = "3020"
                }   
            });
            nodeContainer.AddPortMappings(new PortMapping
            {
                ContainerPort = 3020
            });
            nodeRepository.GrantPull(nodeTaskDefinition.ExecutionRole);

            var dotnetService = new FargateService(this, "DotnetApiService", new FargateServiceProps
            {
                ServiceName = "fargate-ecs-poc-dotnet-api-service",
                Cluster = cluster,
                TaskDefinition = dotnetTaskDefinition,
                DesiredCount = 1,
            });

            /*
            Uncomment below to create an Application Load Balanced Fargate Service instead of a plain Fargate Service. This will create an ALB in front of the service and automatically register the tasks to the target group. The ALB will be internet-facing and will listen on port 80. The health check will be configured to check the /api/health endpoint of the application.
            */
            // var dotnetService = new ApplicationLoadBalancedFargateService(this, "DotnetApiALBService", new ApplicationLoadBalancedFargateServiceProps
            // {
            //     ServiceName = "fargate-ecs-poc-dotnet-api-alb-service",
            //     Cluster = cluster,
            //     TaskDefinition = dotnetTaskDefinition,
            //     DesiredCount = 1,
            //     PublicLoadBalancer = true,
            //     ListenerPort = 80,
            //     AssignPublicIp = true,
            //     HealthCheckGracePeriod = Duration.Seconds(60)
            // });
            // dotnetService.TargetGroup.ConfigureHealthCheck(new Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck
            // {
            //     Path = "/api/health",
            //     Interval = Duration.Seconds(30),
            //     Timeout = Duration.Seconds(5),
            //     HealthyThresholdCount = 2,
            //     UnhealthyThresholdCount = 5,
            //     HealthyHttpCodes = "200-399"
            // });
            
            var nodeService = new FargateService(this, "NodeApiService", new FargateServiceProps
            {
                ServiceName = "fargate-ecs-poc-node-api-service",
                Cluster = cluster,
                TaskDefinition = nodeTaskDefinition,
                DesiredCount = 1,
            });

            #region Outputs
            _ = new CfnOutput(this, "GitHubActionsRoleArn", new CfnOutputProps
            {
                ExportName = "GitHubActionsRoleArn",
                Value = githubRole.RoleArn,
                Description = "ARN of the IAM Role for GitHub Actions"
            });

            _ = new CfnOutput(this, "DotnetRepositoryUri", new CfnOutputProps
            {
                ExportName = "DotnetRepositoryUri",
                Value = dotnetRepository.RepositoryUri,
                Description = "URI of the ECR repository for the .NET API"
            });

            _ = new CfnOutput(this, "NodeRepositoryUri", new CfnOutputProps
            {
                ExportName = "NodeRepositoryUri",
                Value = nodeRepository.RepositoryUri,
                Description = "URI of the ECR repository for the Node.js API"
            });

            _ = new CfnOutput(this, "DotnetApiServiceName", new CfnOutputProps
            {
                ExportName = "DotnetApiServiceName",
                Value = dotnetService.ServiceName,
                Description = "Name of the Fargate service for the .NET API"
            });

            _ = new CfnOutput(this, "NodeApiServiceName", new CfnOutputProps
            {
                ExportName = "NodeApiServiceName",
                Value = nodeService.ServiceName,
                Description = "Name of the Fargate service for the Node.js API"
            });
            
            _ = new CfnOutput(this, "ClusterName", new CfnOutputProps
            {
                ExportName = "ClusterName",
                Value = cluster.ClusterName,
                Description = "Name of the ECS Cluster"
            });

            _ = new CfnOutput(this, "VpcId", new CfnOutputProps
            {
                ExportName = "VpcId",
                Value = vpc.VpcId,
                Description = "ID of the VPC"
            });

            _ = new CfnOutput(this, "DotnetTaskDefinitionArn", new CfnOutputProps
            {
                ExportName = "DotnetTaskDefinitionArn",
                Value = dotnetTaskDefinition.TaskDefinitionArn,
                Description = "ARN of the Task Definition for the .NET API"
            });

            _ = new CfnOutput(this, "NodeTaskDefinitionArn", new CfnOutputProps
            {
                ExportName = "NodeTaskDefinitionArn",
                Value = nodeTaskDefinition.TaskDefinitionArn,
                Description = "ARN of the Task Definition for the Node.js API"
            });
            
            _ = new CfnOutput(this, "DotnetLogGroupName", new CfnOutputProps
            {
                ExportName = "DotnetLogGroupName",
                Value = dotnetLogGroup.LogGroupName,
                Description = "Name of the CloudWatch Log Group for the .NET API"
            });

            _ = new CfnOutput(this, "NodeLogGroupName", new CfnOutputProps
            {
                ExportName = "NodeLogGroupName",
                Value = nodeLogGroup.LogGroupName,
                Description = "Name of the CloudWatch Log Group for the Node.js API"
            });

            _ = new CfnOutput(this, "GitHubOIDCProviderArn", new CfnOutputProps
            {
                ExportName = "GitHubOIDCProviderArn",
                Value = githubOidcProvider.OpenIdConnectProviderArn,
                Description = "ARN of the GitHub OIDC Provider"
            });

            _ = new CfnOutput(this, "GitHubRoleName", new CfnOutputProps
            {
                ExportName = "GitHubRoleName",
                Value = githubRole.RoleName,
                Description = "Name of the IAM Role for GitHub Actions"
            });

            _ = new CfnOutput(this, "GitHubRoleArn", new CfnOutputProps
            {
                ExportName = "GitHubRoleArn",
                Value = githubRole.RoleArn,
                Description = "ARN of the IAM Role for GitHub Actions"
            });
            #endregion
        }
    }
}
