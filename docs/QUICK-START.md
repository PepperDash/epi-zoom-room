# Quick Start Guide

Get the epi-bic-zoomroom project cloned, built, and ready for development in about 10 minutes.

## Prerequisites

- **Git** - Download from [git-scm.com](https://git-scm.com/)
- **.NET 8 SDK** - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0)
- **GitHub Account** - With read access to pepperdash-beincourt organization

### Optional (for deployment testing)
- **Visual Studio 2022** - For IDE-based building/debugging
- **GitHub CLI** - For easier repository management

---

## Step 1: Clone the Repository

```bash
cd C:\Users\ChrisVance\Documents\GitHub\Beincourt\CourtControl\Pv2

git clone https://github.com/pepperdash-beincourt/epi-bic-zoomroom.git
cd epi-bic-zoomroom
```

### Expected Output
```
Cloning into 'epi-bic-zoomroom'...
remote: Enumerating objects: 150, done.
...
Resolving deltas: 100% (50/50), done.
```

---

## Step 2: Verify Prerequisites

```bash
# Check .NET version
dotnet --version
# Expected: 8.0.100 or higher

# Check Git
git --version
# Expected: git version 2.x.x
```

---

## Step 3: Set Up GitHub Authentication for NuGet

The project depends on packages from the **PepperDash GitHub Packages feed**, which requires authentication.

### Option A: GitHub CLI (Recommended)

```bash
# If you have GitHub CLI installed
gh auth login
# Follow the prompts to authenticate with your GitHub account
```

### Option B: Manual NuGet Configuration

1. Create a **Personal Access Token (PAT)** at https://github.com/settings/tokens
   - Scopes needed: `read:packages`
   - Save the token securely

2. Add NuGet source with credentials:
   ```bash
   dotnet nuget add source https://nuget.pkg.github.com/pepperdash/index.json \
     --name github \
     --username YOUR_GITHUB_USERNAME \
     --password YOUR_PAT_TOKEN \
     --store-password-in-clear-text
   ```

3. Verify the source was added:
   ```bash
   dotnet nuget list source
   ```

---

## Step 4: Build the Project

```bash
cd C:\Users\ChrisVance\Documents\GitHub\Beincourt\CourtControl\Pv2\epi-bic-zoomroom

dotnet build -c Release
```

### Expected Output
```
Microsoft (R) Build Engine version 17.x.x
...
Build succeeded in X.XXs
```

### Troubleshooting Build Failures

| Error | Solution |
|-------|----------|
| `error: The source specified has already been added` | NuGet source already exists; skip the add step |
| `error: NuGet restore failed` | Check GitHub authentication is configured |
| `error: The project file could not be loaded` | Ensure .NET 8 SDK is installed |
| `error: PepperDash.ZoomRoom.Sdk could not be resolved` | GitHub Packages authentication issue; retry the auth setup |

See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for more help.

---

## Step 5: Verify the Build Output

The compiled plugin should be in:
```
C:\Users\ChrisVance\Documents\GitHub\Beincourt\CourtControl\Pv2\epi-bic-zoomroom\src\bin\Release\
```

Look for the `.cplz` file (Crestron plugin package).

---

## Step 6: Familiarize Yourself with the Repository

Now that you're set up, explore these key files:

### Essential Reading
1. **[ARCHITECTURE.md](ARCHITECTURE.md)** - Understand the system design (5 min read)
2. **[FORK-STRATEGY.md](FORK-STRATEGY.md)** - How this fork is maintained (5 min read)
3. **[../BEINCOURT.md](../BEINCOURT.md)** - The specific customization in this fork (5 min read)

### Code Exploration
- **`src/ZoomRoom.cs`** - Main plugin class (start here)
- **`src/ZoomRoomJoinMap.cs`** - SIMPL bridge integration
- **`src/Controller/ZrcSdkController.cs`** - SDK communication

---

## Step 7: Next Steps

### I want to...

**Make a code change**
→ Read [DEVELOPMENT.md](DEVELOPMENT.md) for coding conventions and workflow

**Deploy to CP4N hardware**
→ Read [DEPLOYMENT.md](DEPLOYMENT.md)

**Understand the CI/CD pipeline**
→ Read [CI-CD-PIPELINE.md](CI-CD-PIPELINE.md)

**Contribute a fix back upstream**
→ Read [CONTRIBUTING.md](CONTRIBUTING.md)

**Troubleshoot an issue**
→ Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

---

## Working with Branches

The main working branch is `csv-zoom-sandbox`:

```bash
# Switch to the main working branch
git checkout csv-zoom-sandbox

# Keep it up to date with the remote
git pull origin csv-zoom-sandbox

# Create a feature branch for your work
git checkout -b feature/my-feature csv-zoom-sandbox

# Push your branch
git push -u origin feature/my-feature
```

See [FORK-STRATEGY.md](FORK-STRATEGY.md) for detailed branching strategy.

---

## Running Tests (Optional)

```bash
dotnet test -c Release
```

---

## Building in Visual Studio (Alternative)

If you prefer using Visual Studio:

1. Open `epi-zoom-room.4Series.sln`
2. Wait for NuGet restore (it will ask for authentication)
3. Build → Build Solution (or press Ctrl+Shift+B)

---

## Quick Reference: Common Commands

```bash
# Build the project
dotnet build -c Release

# Run tests
dotnet test -c Release

# Clean build artifacts
dotnet clean

# Update all NuGet packages
dotnet add package --update-all

# Check what branches are available
git branch -a

# Create a new branch
git checkout -b feature/your-feature-name

# Commit your work
git add .
git commit -m "feat: description of what changed"

# Push to your feature branch
git push -u origin feature/your-feature-name
```

---

## Need Help?

1. **Build issues?** → [TROUBLESHOOTING.md](TROUBLESHOOTING.md#building)
2. **Setup questions?** → [GLOSSARY.md](GLOSSARY.md)
3. **Not sure what to do?** → Read [DEVELOPMENT.md](DEVELOPMENT.md)
4. **Still stuck?** → Contact Chris Vance (Pepperdash Beincourt)

---

## What's Next?

You're now ready to:
- ✅ Build the project locally
- ✅ Make code changes
- ✅ Run tests
- ✅ Understand the fork strategy

Proceed to [DEVELOPMENT.md](DEVELOPMENT.md) to learn about:
- Code structure and organization
- Coding standards and conventions
- Development workflow
- How to create a pull request

---

**Last Updated**: 2026-08-10
**Estimated Setup Time**: 10 minutes (first time: 15-20 minutes with auth setup)
