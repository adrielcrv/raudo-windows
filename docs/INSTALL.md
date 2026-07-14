# Instalación de Raudo

## Instalación directa

1. Descarga [`Raudo-win.exe`](https://github.com/adrielcrv/raudo-windows/releases/latest/download/Raudo-win.exe).
2. Abre el ejecutable.
3. Selecciona **Instalar en esta PC**.

Raudo copia el mismo ejecutable a
`%LOCALAPPDATA%\Programs\Raudo\Raudo.exe`, verifica que su suma SHA-256 no haya
cambiado y crea un acceso directo en el menú Inicio. La operación se limita al
perfil del usuario, no requiere elevación y no instala servicios, controladores o
procesos auxiliares.

La opción **Usar sin instalar** conserva el comportamiento portable. También se
publica un ZIP para quienes prefieren revisar el contenido antes de abrirlo.

## Verificación

Cada publicación incluye una suma SHA-256 para el ejecutable estable. Después de
descargar ambos archivos en la misma carpeta:

```powershell
$expected = (Get-Content .\Raudo-win.exe.sha256 -Raw).Split()[0]
$actual = (Get-FileHash .\Raudo-win.exe -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) { throw 'La suma SHA-256 no coincide.' }
```

Las publicaciones generadas por GitHub Actions incluyen además una certificación
de procedencia verificable desde GitHub.

## Automatización

El ejecutable ofrece una interfaz limitada para despliegues repetibles:

```powershell
$process = Start-Process .\Raudo-win.exe `
    -ArgumentList '--install', '--no-launch' `
    -Wait `
    -PassThru
if ($process.ExitCode -ne 0) {
    throw "La instalación terminó con código $($process.ExitCode)."
}
```

Argumentos admitidos:

- `--install`: instala en la ubicación fija del usuario.
- `--no-launch`: no abre Raudo al terminar.
- `--desktop-shortcut`: agrega también un acceso en el escritorio.

No se aceptan rutas, comandos o direcciones de descarga como argumentos. Una
solicitud con parámetros desconocidos termina con código `64` sin modificar la
instalación.

## Windows SmartScreen

Las publicaciones actuales ofrecen sumas SHA-256 y procedencia de compilación,
pero todavía no llevan una firma Authenticode de identidad verificada. Windows
puede mostrar una advertencia de reputación para una descarga nueva. No es
necesario desactivar Microsoft Defender ni cambiar las políticas de seguridad del
equipo; verifica el archivo con la suma publicada y sigue las reglas de tu
organización. Consulta [Firma de código](../CODE_SIGNING.md) para conocer la
política del proyecto.
