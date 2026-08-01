# Multi-Targeting Architecture Strategy

## Overview

Yes! A single project (`Gedcom.Vector.csproj`) can target `net8.0`, `netstandard2.1`, and `netstandard2.0` simultaneously using .NET SDK **Multi-Targeting**.

When compiled or packed, MSBuild builds three separate assemblies and bundles them into a **single NuGet package (`.nupkg`)**:

```
Gedcom.Vector.1.4.0.nupkg
└── lib/
    ├── net8.0/
    │   └── Gedcom.Vector.dll        (Full SIMD, SearchValues, Ascii.FromUtf16)
    ├── netstandard2.1/
    │   └── Gedcom.Vector.dll        (Native Span<T>, BCL Stream/Encoding Spans)
    └── netstandard2.0/
        └── Gedcom.Vector.dll        (System.Memory, unsafe fixed pointer pinning)
```

---

## 1. How NuGet Framework Resolution Works

When a consumer installs `Gedcom.Vector` via `dotnet add package Gedcom.Vector`, NuGet inspects the consuming project's target framework and **automatically selects the best matching assembly**:

| Consuming Project Target | Resolved Assembly Path | Performance Profile |
| :--- | :--- | :--- |
| **.NET 8 / .NET 9+ App** | `lib/net8.0/Gedcom.Vector.dll` | **100% Maximum Performance** (AVX-512 SIMD, zero fallback penalty) |
| **.NET 6 / .NET Core 3.1 / Unity 2021+** | `lib/netstandard2.1/Gedcom.Vector.dll` | **High Performance** (~5–15% fallback impact) |
| **.NET Framework 4.6.1–4.8.1 / Legacy UWP** | `lib/netstandard2.0/Gedcom.Vector.dll` | **Compatibility Mode** (~30–60% slow-span impact, zero extra allocations) |

---

## 2. Project File (`Gedcom.Vector.csproj`) Implementation

To configure multi-targeting, we update `Gedcom.Vector.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- Multi-target all three frameworks -->
    <TargetFrameworks>net8.0;netstandard2.1;netstandard2.0</TargetFrameworks>
    
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <!-- Conditional package dependencies for netstandard2.0 -->
  <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
    <PackageReference Include="System.Memory" Version="4.5.5" />
  </ItemGroup>

</Project>
```

---

## 3. Preprocessor Symbols & Conditional Code Organization

The SDK automatically defines target symbols during build compilation:

| Framework Target | Defined Preprocessor Symbols |
| :--- | :--- |
| **`net8.0`** | `NET`, `NET8_0`, `NET8_0_OR_GREATER`, `NETSTANDARD2_1_OR_GREATER` |
| **`netstandard2.1`** | `NETSTANDARD`, `NETSTANDARD2_1`, `NETSTANDARD2_1_OR_GREATER` |
| **`netstandard2.0`** | `NETSTANDARD`, `NETSTANDARD2_0` |

### Cleaner Code Strategy with Extension Helpers

Instead of scattering `#if` blocks throughout core parsing code, we encapsulate target-specific implementations in internal static helper classes:

#### `AsciiHelper.cs`
```csharp
internal static class AsciiHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(ReadOnlySpan<char> span)
    {
#if NET8_0_OR_GREATER
        return System.Text.Ascii.IsValid(span);
#else
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] > 0x7F) return false;
        }
        return true;
#endif
    }
}
```

#### `EncodingHelper.cs`
```csharp
internal static class EncodingHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBytes(Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
    {
#if NETSTANDARD2_0
        unsafe
        {
            fixed (char* pChars = chars)
            fixed (byte* pBytes = bytes)
            {
                return encoding.GetBytes(pChars, chars.Length, pBytes, bytes.Length);
            }
        }
#else
        return encoding.GetBytes(chars, bytes);
#endif
    }
}
```

---

## 4. Key Benefits

1. **Zero Compromise for .NET 8+**: Modern applications get the maximum possible SIMD and JIT speed without any runtime abstraction or interface overhead.
2. **Single Package Distribution**: Consumers only ever install `Gedcom.Vector`; NuGet handles framework selection transparently.
3. **Clean Code Maintenance**: Helper abstractions isolate framework differences to small, testable utility classes.
