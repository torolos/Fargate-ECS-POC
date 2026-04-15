This file defines an **AWS CDK stack** that sets up **GitHub Actions → AWS authentication via OIDC** (no long‑lived AWS access keys), and then creates an **IAM Role** that GitHub workflows in a specific repo are allowed to assume.

## What the stack contains

### 1) `GitHubOidcStack : Stack`
- This is a CDK `Stack` in the `Infrastructure` namespace.
- It exposes a property:
  - `public Role GitHubActionsRole { get; }`  
  so other stacks can reference the role if needed.

### 2) Constructor parameters
```csharp
internal GitHubOidcStack(Construct scope, string id, string githubRepo, IStackProps props = null)
```
- `githubRepo` is important: it is used to **restrict which GitHub repository** is allowed to assume the role.
  - Expected format is typically `owner/repo` (e.g. `torolos/Fargate-ECS-POC`).

### 3) Tags
```csharp
Tags.Of(this).Add("CreatedBy", "dtorolopoulos");
Tags.Of(this).Add("Purpose", "POC");
```
Adds CloudFormation tags to resources in this stack.

### 4) OIDC Provider for GitHub Actions
```csharp
var oidcProvider = new OpenIdConnectProvider(...){
  Url = "https://token.actions.githubusercontent.com",
  ClientIds = ["sts.amazonaws.com"]
};
```
This creates an IAM OIDC identity provider representing GitHub Actions’ token issuer:
- **Issuer URL**: `https://token.actions.githubusercontent.com`
- Allowed client id (`aud`): `sts.amazonaws.com` (AWS STS)

This is the standard AWS/GitHub OIDC integration point.

### 5) IAM Role that GitHub can assume
```csharp
GitHubActionsRole = new Role(...){
  RoleName = "github-actions-role",
  AssumedBy = new FederatedPrincipal(..., "sts:AssumeRoleWithWebIdentity"),
  Description = "Role assumed by GitHub Actions via OIDC",
};
```

#### Trust policy conditions (the important security part)
The `FederatedPrincipal` sets up the role trust relationship to allow `sts:AssumeRoleWithWebIdentity`, but only when GitHub’s OIDC token matches these conditions:

- **Audience must match STS**:
  ```csharp
  ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com"
  ```
- **Subject must match your repo**:
  ```csharp
  ["token.actions.githubusercontent.com:sub"] = $"repo:{githubRepo}:*"
  ```
Meaning: only workflows whose `sub` claim starts with `repo:<owner>/<repo>:` can assume the role.

> Note: `:*` is broad: it allows any ref/environment pattern for that repo (branches, tags, environments depending on how the token is minted). Tightening this later is common.

### 6) Permissions granted to the role (very broad right now)
```csharp
GitHubActionsRole.AddManagedPolicy(
  ManagedPolicy.FromAwsManagedPolicyName("AdministratorAccess"));
```
This attaches AWS managed policy **AdministratorAccess**. This is convenient for a POC, but it effectively gives GitHub Actions full AWS access in the account.

### 7) CloudFormation outputs
The stack exports:
- `GitHubOIDCProviderArn` (OIDC provider ARN)
- `GitHubRoleName` (role name)
- `GitHubRoleArn` (role ARN)

These outputs help you reference the role/provider from:
- other stacks (via exports), or
- GitHub workflow configuration (role ARN to assume)

## In plain terms: what this enables
After deploying this stack, you can configure a GitHub Actions workflow to use AWS OIDC (e.g., `aws-actions/configure-aws-credentials`) to assume `github-actions-role`, and it will succeed **only** for the repository passed into `githubRepo`.

----------------------------------------------------------------------------------

Here’s how the **GitHub Actions OIDC provider works in AWS**, specifically as it’s defined in your `GitHubOidcStack.cs`.

## 1) What “OIDC Provider” means in AWS IAM

In AWS IAM, an **OIDC provider** is an IAM resource that says:

- “I trust tokens issued by **this URL** (issuer)”
- “and I will accept them only for these **audiences** (client IDs)”

In your stack, this is created here:

- **Issuer URL**: `https://token.actions.githubusercontent.com`
- **ClientIds**: `sts.amazonaws.com`

So AWS is being told: “GitHub Actions can mint OIDC tokens, and AWS STS is the audience we’ll accept.”

## 2) What GitHub Actions actually does at runtime

When a workflow runs, GitHub can mint a **short-lived JWT (OIDC token)** for that job (when the workflow requests `id-token: write` permission).

That JWT includes claims like:

- `iss` (issuer): `https://token.actions.githubusercontent.com`
- `aud` (audience): often set to `sts.amazonaws.com` (or a custom one if you request it)
- `sub` (subject): identifies *what workload this is*, typically containing the repo and more context (ref, environment, etc.)

## 3) How AWS uses that token (STS AssumeRoleWithWebIdentity)

AWS does **not** directly “log in” to GitHub.

Instead, the workflow calls AWS STS:

- **API**: `AssumeRoleWithWebIdentity`
- Inputs:
  - the **Role ARN** (your `github-actions-role`)
  - the **OIDC token** from GitHub (JWT)

STS then verifies all of this:

1. The token’s `iss` matches an IAM OIDC provider you configured
2. The token signature is valid (GitHub’s public keys)
3. The token isn’t expired
4. The token’s claims satisfy the **role trust policy conditions**

If it passes, STS returns temporary AWS credentials (AccessKeyId/Secret/SessionToken) scoped by the role’s permissions.

## 4) What your role trust policy is enforcing (the key part)

Your role is created with this trust relationship:

### 4.1 It must be assumed via the GitHub OIDC provider
```csharp
oidcProvider.OpenIdConnectProviderArn
```

So only tokens issued by GitHub Actions (per that provider) are even considered.

### 4.2 The audience must be exactly STS
```csharp
["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com"
```

This is important because GitHub tokens can be minted for different audiences. Your policy says: only accept tokens minted “for AWS STS”.

### 4.3 The subject must be for *your* repo
```csharp
["token.actions.githubusercontent.com:sub"] = $"repo:{githubRepo}:*"
```

This is your main guardrail. It means:

- Only workflows whose `sub` starts with `repo:<githubRepo>:` can assume the role
- So if `githubRepo` is `torolos/Fargate-ECS-POC`, then only tokens whose `sub` begins with:
  - `repo:torolos/Fargate-ECS-POC:...`
  will match

This prevents other repos (even under the same GitHub user/org) from using your AWS role.

## 5) Why AWS needs *both* the OIDC provider and the role

- The **OIDC provider** tells AWS *where tokens come from* and what audiences are valid.
- The **role trust policy** tells AWS *which tokens are allowed to assume this specific role* (via conditions on `aud`/`sub`).

You need both:
- Provider = identity source registration
- Role trust policy = authorization rules for assumption

## 6) A context-specific note about security in your stack

The `AdministratorAccess` policy is currently attached to the `github-actions-role`, so once a job from the allowed repo can assume this role, it has effectively full AWS permissions. That’s fine for a POC, but in production you typically tighten in two places:

1) **Trust policy** (narrow the `sub`):
- only a specific branch (e.g., `refs/heads/main`)
- or only a GitHub “environment”
- or only specific workflows

2) **Permissions policy** (replace `AdministratorAccess`):
- minimum set for ECR push, ECS deploy, CloudFormation/CDK deploy, etc.
