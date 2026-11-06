#if (UNITY_EDITOR) || WAKATIME_STANDALONE_TESTS

using System;
using System.IO;

namespace WakaTime {
  /// <summary>
  /// Picks the WakaTime project identity for Unity template copies.
  /// Player Settings productName stays "My project" after folder copies, so folder name wins.
  /// </summary>
  public static class ProjectNameResolver {
    public static string FolderNameFromProjectRoot(string projectRoot) {
      if (string.IsNullOrWhiteSpace(projectRoot)) {
        return string.Empty;
      }

      var trimmed = projectRoot.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      if (trimmed.Length == 0) {
        return string.Empty;
      }

      return Path.GetFileName(trimmed) ?? string.Empty;
    }

    public static string[] BuildRewrittenProjectFile(string[] existingLines, string resolvedName) {
      if (existingLines == null || existingLines.Length == 0) {
        return new[] { resolvedName };
      }

      var copy = new string[existingLines.Length];
      Array.Copy(existingLines, copy, existingLines.Length);
      copy[0] = resolvedName;
      return copy;
    }

    public static bool IsGenericCopiedTemplateName(string name) {
      if (string.IsNullOrWhiteSpace(name)) {
        return true;
      }

      var trimmed = name.Trim();
      const string prefix = "My project";
      if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
        return false;
      }

      var rest = trimmed.Substring(prefix.Length);
      if (rest.Length == 0) {
        return true;
      }

      if (rest.StartsWith(" (", StringComparison.Ordinal) && rest.EndsWith(")", StringComparison.Ordinal)) {
        var inner = rest.Substring(2, rest.Length - 3);
        return inner.Length > 0 && IsDigits(inner);
      }

      return IsVersionSuffix(rest);
    }

    public static string Resolve(string projectFileFirstLine, string productName, string projectFolderName) {
      var fromFile = TrimOrEmpty(projectFileFirstLine);
      if (fromFile.Length > 0 && !IsGenericCopiedTemplateName(fromFile)) {
        return fromFile;
      }

      var folder = TrimOrEmpty(projectFolderName);
      if (folder.Length > 0) {
        return folder;
      }

      var product = TrimOrEmpty(productName);
      if (product.Length > 0 && !IsGenericCopiedTemplateName(product)) {
        return product;
      }

      return product;
    }

    public static bool ShouldRewriteProjectFile(string projectFileFirstLine, string projectFolderName, string resolvedName) {
      if (string.IsNullOrWhiteSpace(resolvedName)) {
        return false;
      }

      if (IsGenericCopiedTemplateName(projectFolderName)) {
        return false;
      }

      var fromFile = TrimOrEmpty(projectFileFirstLine);
      if (fromFile.Length == 0) {
        return true;
      }

      return IsGenericCopiedTemplateName(fromFile);
    }

    private static string TrimOrEmpty(string value) {
      return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool IsDigits(string value) {
      for (var i = 0; i < value.Length; i++) {
        if (!char.IsDigit(value[i])) {
          return false;
        }
      }

      return true;
    }

    private static bool IsVersionSuffix(string value) {
      var hasDigit = false;
      for (var i = 0; i < value.Length; i++) {
        var c = value[i];
        if (char.IsDigit(c)) {
          hasDigit = true;
          continue;
        }

        if (c != '.') {
          return false;
        }
      }

      return hasDigit;
    }
  }
}

#endif
