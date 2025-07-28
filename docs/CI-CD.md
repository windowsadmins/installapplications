# CI/CD Pipeline Documentation

This document describes the Continuous Integration and Continuous Deployment (CI/CD) pipeline for the InstallApplications project.

## Pipeline Overview

The CI/CD pipeline is implemented using GitHub Actions and consists of several workflows that ensure code quality, security, and reliable deployments.

### Workflows

#### 1. Main CI/CD Pipeline (`build.yml`)
- **Trigger**: Push to `main`/`develop`, Pull Requests, Tags
- **Jobs**:
  - **Code Quality**: Static analysis, formatting checks, security scanning
  - **Build & Test**: Multi-architecture builds (x64, ARM64) with test matrix
  - **Build Artifacts**: Release builds for deployment
  - **Integration Tests**: Smoke testing of built artifacts
  - **Release**: Automated GitHub releases for tagged versions

#### 2. Security Scanning (`security.yml`)
- **Trigger**: Daily schedule, Push to `main`, Pull Requests
- **Features**:
  - Dependency vulnerability scanning
  - SAST with CodeQL and Semgrep
  - License compliance checking
  - Security advisory monitoring

#### 3. Performance Testing (`performance.yml`)
- **Trigger**: Push to `main`, Pull Requests, Weekly schedule
- **Features**:
  - Startup time benchmarks
  - Memory usage profiling
  - Performance regression detection
  - Historical metrics tracking

#### 4. Dependency Management (`dependabot-auto-merge.yml`)
- **Trigger**: Dependabot Pull Requests
- **Features**:
  - Auto-approval of minor/patch updates
  - Auto-merge for low-risk dependency updates
  - Security update prioritization

## Build Matrix

The pipeline builds for multiple configurations:

| Configuration | Architecture | Use Case |
|---------------|--------------|----------|
| Debug         | x64          | Development testing |
| Debug         | ARM64        | ARM development |
| Release       | x64          | Production deployment |
| Release       | ARM64        | ARM production |

## Artifacts

### Build Artifacts
- **Single-file executables**: Self-contained .exe files
- **Deployment packages**: ZIP files with all dependencies
- **Debug symbols**: For troubleshooting (Debug builds only)

### Test Artifacts
- **Test results**: TRX format for test reporting
- **Code coverage**: Cobertura XML format
- **Performance metrics**: JSON format for tracking

## Quality Gates

The pipeline enforces several quality gates:

### Code Quality
- [ ] All tests pass
- [ ] Code coverage > 80%
- [ ] No code formatting violations
- [ ] No static analysis warnings
- [ ] No security vulnerabilities

### Performance
- [ ] Startup time < 2 seconds
- [ ] Memory usage < 100MB
- [ ] No performance regressions

### Security
- [ ] No vulnerable dependencies
- [ ] No secrets in code
- [ ] License compliance verified
- [ ] Security scanning passed

## Deployment Process

### Development Builds
1. Every push to `develop` triggers a full build
2. Artifacts are stored for 30 days
3. Integration tests run automatically

### Production Releases
1. Create a tag in format `v1.2.3`
2. Full pipeline runs automatically
3. GitHub release created with artifacts
4. Release notes generated automatically

### Hotfix Process
1. Create hotfix branch from `main`
2. Make necessary changes
3. Create PR to `main`
4. After merge, tag the release

## Local Development

### Prerequisites
- .NET 8.0 SDK
- PowerShell 7+ (for build scripts)
- Git

### Running the Pipeline Locally

```powershell
# Restore dependencies
dotnet restore

# Run code formatting
dotnet format

# Build all configurations
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release

# Create release artifacts
.\build.ps1 -Architecture both -Sign
```

### Build Script Usage

The `build.ps1` script provides local build capabilities:

```powershell
# Build for specific architecture
.\build.ps1 -Architecture x64

# Build and sign (requires certificate)
.\build.ps1 -Sign

# Clean build
.\build.ps1 -Clean

# Run tests
.\build.ps1 -Test
```

## Environment Variables

The pipeline uses these environment variables and secrets:

### Required Secrets
- `GITHUB_TOKEN`: Automatically provided by GitHub
- `CODECOV_TOKEN`: For code coverage reporting (optional)

### Optional Secrets
- `SEMGREP_APP_TOKEN`: For advanced security scanning

## Monitoring and Notifications

### Build Status
- Build status badges in README
- GitHub status checks for PRs
- Email notifications for failed builds

### Security Alerts
- Dependabot security advisories
- CodeQL security findings
- License compliance violations

## Troubleshooting

### Common Issues

#### Build Failures
1. Check .NET SDK version compatibility
2. Verify NuGet package restore
3. Review dependency conflicts

#### Test Failures
1. Check test environment setup
2. Verify test data availability
3. Review test isolation issues

#### Security Scan Failures
1. Review detected vulnerabilities
2. Update dependencies if needed
3. Add suppressions for false positives

### Getting Help
- Check workflow logs in GitHub Actions
- Review build script output
- Contact the development team

## Maintenance

### Regular Tasks
- [ ] Review dependency updates weekly
- [ ] Update security scan rules monthly
- [ ] Performance baseline updates quarterly
- [ ] Pipeline optimization reviews annually

### Version Updates
- Update .NET SDK version in workflows
- Update GitHub Actions versions
- Update build tools and analyzers
