open Xunit
open FsCheck
open FsCheck.Xunit
open System.Text.RegularExpressions
open KTCrawler

module Tests =

    // TODO: Write more tests.

    [<Property>]
    let ``Leading slash indicates relative path`` (url:NonNull<string>) =
        Assert.Equal(Regex.IsMatch(url.ToString(), "^/"), UrlChecks.IsRelativePath(url.ToString()))

    [<Property>]
    let ``Subdomain only contains alphanumeric characters`` (url:NonNull<string>) =
        Assert.True(Regex.IsMatch(UrlChecks.GetSubdomain(url.ToString()), "[a-zA-Z0-9]"))
    
    // Requires generator to test more relevant strings rather than random
    [<Property>]
    let ``URL passed with http returns with http`` (url:NonNull<string>) =
        match url.ToString() with
        | u when u.StartsWith("http") -> Assert.StartsWith("http", UrlChecks.GetDomain(url.ToString()))
        | _ -> Assert.True(true)

    // Fails on unicode escape chars, rewrite method to change unicode \ to % ?
    [<Property>]
    let ``Sanitised URL contains no HTML escape characters`` (url:NonNull<string>) =
        Assert.DoesNotContain(@"\", UrlChecks.SanitiseUrl(url.ToString(), ""))
