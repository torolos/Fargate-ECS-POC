using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;
using System.Collections.Generic;

namespace Infrastructure
{
    public class GitHubOidcStack : Stack
    {
        public Role GitHubActionsRole { get; }

        internal GitHubOidcStack(Construct scope, string id, string githubRepo, IStackProps props = null)
            : base(scope, id, props)
        {
            Amazon.CDK.Tags.Of(this).Add("CreatedBy", "dtorolopoulos");
            Amazon.CDK.Tags.Of(this).Add("Purpose", "POC");

            var oidcProvider = new OpenIdConnectProvider(this, "GitHubOIDCProvider",
                new OpenIdConnectProviderProps
                {
                    Url = "https://token.actions.githubusercontent.com",
                    ClientIds = ["sts.amazonaws.com"]
                });

            GitHubActionsRole = new Role(this, "GitHubActionsRole", new RoleProps
            {
                RoleName = "github-actions-role",
                AssumedBy = new FederatedPrincipal(
                    oidcProvider.OpenIdConnectProviderArn,
                    new Dictionary<string, object>
                    {
                        ["StringEquals"] = new Dictionary<string, string>
                        {
                            ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com"
                        },
                        ["StringLike"] = new Dictionary<string, string>
                        {
                            ["token.actions.githubusercontent.com:sub"] = $"repo:{githubRepo}:*"
                        }
                    },
                    "sts:AssumeRoleWithWebIdentity"
                ),
                Description = "Role assumed by GitHub Actions via OIDC",
            });

            // Grants the permissions needed by all GitHub Actions workflows in this repo.
            // Scope these down further once everything is working.
            GitHubActionsRole.AddManagedPolicy(
                ManagedPolicy.FromAwsManagedPolicyName("AdministratorAccess"));

            _ = new CfnOutput(this, "GitHubOIDCProviderArn", new CfnOutputProps
            {
                ExportName = "GitHubOIDCProviderArn",
                Value = oidcProvider.OpenIdConnectProviderArn,
                Description = "ARN of the GitHub OIDC Provider"
            });

            _ = new CfnOutput(this, "GitHubRoleName", new CfnOutputProps
            {
                ExportName = "GitHubRoleName",
                Value = GitHubActionsRole.RoleName,
                Description = "Name of the IAM Role for GitHub Actions"
            });

            _ = new CfnOutput(this, "GitHubRoleArn", new CfnOutputProps
            {
                ExportName = "GitHubRoleArn",
                Value = GitHubActionsRole.RoleArn,
                Description = "ARN of the IAM Role for GitHub Actions"
            });
        }
    }
}
