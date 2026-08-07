using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class PrivacyPolicyLinkTests
{
    [Fact]
    public void PrivacyPolicyUsesThePublishedHttpsPage()
    {
        Assert.Equal("https", RepositoryLinks.PrivacyPolicy.Scheme);
        Assert.Equal("kiyoujyo.github.io", RepositoryLinks.PrivacyPolicy.Host);
        Assert.Equal("/UrbanPlanToolbox/privacy/", RepositoryLinks.PrivacyPolicy.AbsolutePath);
        Assert.True(ExternalLinkService.IsSafeHttpUri(RepositoryLinks.PrivacyPolicy.ToString(), out var uri));
        Assert.Equal(RepositoryLinks.PrivacyPolicy, uri);
    }
}
