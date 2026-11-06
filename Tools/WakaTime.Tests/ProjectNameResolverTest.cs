using System;
using WakaTime;

internal static class ProjectNameResolverTest {
  private static int _failures;

  private static void AssertEqual(string expected, string actual, string name) {
    if (expected == actual) {
      Console.WriteLine("PASS " + name);
      return;
    }

    _failures++;
    Console.WriteLine("FAIL " + name + " expected=[" + expected + "] actual=[" + actual + "]");
  }

  private static void AssertEqual(int expected, int actual, string name) {
    if (expected == actual) {
      Console.WriteLine("PASS " + name);
      return;
    }

    _failures++;
    Console.WriteLine("FAIL " + name + " expected=" + expected + " actual=" + actual);
  }

  private static void AssertTrue(bool actual, string name) {
    if (actual) {
      Console.WriteLine("PASS " + name);
      return;
    }

    _failures++;
    Console.WriteLine("FAIL " + name + " expected=true actual=false");
  }

  private static void AssertFalse(bool actual, string name) {
    if (!actual) {
      Console.WriteLine("PASS " + name);
      return;
    }

    _failures++;
    Console.WriteLine("FAIL " + name + " expected=false actual=true");
  }

  public static int Main() {
    AssertTrue(ProjectNameResolver.IsGenericCopiedTemplateName("My project"), "generic default product name");
    AssertTrue(ProjectNameResolver.IsGenericCopiedTemplateName("My project (1)"), "generic unity duplicate name");
    AssertTrue(ProjectNameResolver.IsGenericCopiedTemplateName("My project6.6"), "generic template folder name");
    AssertTrue(ProjectNameResolver.IsGenericCopiedTemplateName("  My project  "), "generic trimmed");
    AssertTrue(ProjectNameResolver.IsGenericCopiedTemplateName(""), "empty is generic");
    AssertTrue(ProjectNameResolver.IsGenericCopiedTemplateName(null), "null is generic");
    AssertFalse(ProjectNameResolver.IsGenericCopiedTemplateName("Unity-Boids-Flocking-Simulation"), "real folder not generic");
    AssertFalse(ProjectNameResolver.IsGenericCopiedTemplateName("My projectiles"), "similar but not default");

    AssertEqual(
      "Unity-Boids-Flocking-Simulation",
      ProjectNameResolver.Resolve("My project", "My project", "Unity-Boids-Flocking-Simulation"),
      "copied template uses folder not productName");

    AssertEqual(
      "Physics-Based-Grappling-Hook-Unity",
      ProjectNameResolver.Resolve(null, "My project", "Physics-Based-Grappling-Hook-Unity"),
      "missing project file uses folder");

    AssertEqual(
      "CustomDash",
      ProjectNameResolver.Resolve("CustomDash", "My project", "Some-Copy"),
      "explicit wakatime-project wins");

    AssertEqual(
      "Some-Copy",
      ProjectNameResolver.Resolve("My project", "Shipped Game Title", "Some-Copy"),
      "generic project file does not override folder");

    AssertEqual(
      "Shipped Game Title",
      ProjectNameResolver.Resolve(null, "Shipped Game Title", ""),
      "custom productName used when folder missing");

    AssertEqual(
      "Folder Name",
      ProjectNameResolver.Resolve("  ", "My project", "Folder Name"),
      "whitespace project file treated as missing");

    AssertTrue(
      ProjectNameResolver.ShouldRewriteProjectFile(null, "Unity-Boids-Flocking-Simulation", "Unity-Boids-Flocking-Simulation"),
      "write project file for real copy without file");

    AssertTrue(
      ProjectNameResolver.ShouldRewriteProjectFile("My project", "Unity-Boids-Flocking-Simulation", "Unity-Boids-Flocking-Simulation"),
      "rewrite generic baked project file on a real copy");

    AssertFalse(
      ProjectNameResolver.ShouldRewriteProjectFile(null, "My project6.6", "My project6.6"),
      "do not bake identity into the template itself");

    AssertFalse(
      ProjectNameResolver.ShouldRewriteProjectFile("CustomDash", "Some-Copy", "CustomDash"),
      "keep explicit custom project name");

    AssertTrue(
      ProjectNameResolver.ShouldRewriteProjectFile("My project6.6", "Unity-Boids-Flocking-Simulation", "Unity-Boids-Flocking-Simulation"),
      "rewrite template folder name baked into a real copy");

    AssertFalse(
      ProjectNameResolver.ShouldRewriteProjectFile(null, "My project", "My project"),
      "do not bake a folder that is still named My project");

    AssertFalse(
      ProjectNameResolver.ShouldRewriteProjectFile(null, "", "My project"),
      "do not rewrite when folder name is empty");

    AssertEqual(
      "Unity-Boids-Flocking-Simulation",
      ProjectNameResolver.FolderNameFromProjectRoot(@"D:\UnityProject\Unity-Boids-Flocking-Simulation\"),
      "folder name ignores trailing slash");

    AssertEqual(
      "",
      ProjectNameResolver.FolderNameFromProjectRoot(""),
      "empty root has no folder name");

    var rewritten = ProjectNameResolver.BuildRewrittenProjectFile(new[] { "My project", "release" }, "Unity-Boids");
    AssertEqual(2, rewritten.Length, "rewrite keeps two lines");
    AssertEqual("Unity-Boids", rewritten[0], "rewrite replaces first line");
    AssertEqual("release", rewritten[1], "rewrite preserves branch override");

    var created = ProjectNameResolver.BuildRewrittenProjectFile(null, "Unity-Boids");
    AssertEqual(1, created.Length, "missing file creates one line");
    AssertEqual("Unity-Boids", created[0], "missing file creates single-line project file");

    if (_failures > 0) {
      Console.WriteLine("FAILED " + _failures);
      return 1;
    }

    Console.WriteLine("ALL PASSED");
    return 0;
  }
}
