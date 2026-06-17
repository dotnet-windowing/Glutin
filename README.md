# Glutin

Experimental C# OpenGL context layer for the Winit port.

The intended split mirrors the Rust ecosystem:

- `Glutin`: OpenGL display, config, context, and surface abstractions over raw window handles.
- `Glutin.Winit`: integration helpers for `Winit` windows and active event loops.
- `Glutin.OpenGL`: pre-generated OpenGL/GLES bindings produced from the Khronos XML registry.
- `GlGenerator`: generator used to refresh the pre-generated `Glutin.OpenGL` bindings.
- `examples/Window`: smoke-test application for future platform backends.

The first real backend should be Win32/WGL, followed by X11/GLX and Wayland/EGL.

Regenerate the bindings with:

```powershell
dotnet run --project .\GlGenerator\GlGenerator.csproj
```
