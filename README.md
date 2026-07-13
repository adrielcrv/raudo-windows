<p align="center">
  <img src="assets/brand/raudo-mark.svg" width="72" height="72" alt="Raudo">
</p>

# Raudo

Raudo reúne utilidades locales y ligeras para Windows en una aplicación de bandeja. Está diseñado para permanecer disponible sin mantener trabajo innecesario en segundo plano.

[![Build](https://github.com/adrielcrv/raudo-windows/actions/workflows/build.yml/badge.svg)](https://github.com/adrielcrv/raudo-windows/actions/workflows/build.yml)
[![CodeQL](https://github.com/adrielcrv/raudo-windows/actions/workflows/codeql.yml/badge.svg)](https://github.com/adrielcrv/raudo-windows/actions/workflows/codeql.yml)

<p align="center">
  <img src="docs/raudo-dark.png" width="540" alt="Interfaz de Raudo en modo oscuro">
</p>

## Funciones

### Mantener activo

- Evita temporalmente que el sistema y la pantalla entren en reposo.
- Genera una entrada mínima únicamente después de 45 segundos sin actividad.
- Restaura el cursor a su posición original.
- Se limita a 15, 30, 60 o 120 minutos.
- Se detiene al bloquear, suspender o cerrar la sesión interactiva de Windows.
- No se activa automáticamente al iniciar la aplicación.

### Recortar pantalla

Abre la Herramienta Recortes mediante el protocolo `ms-screenclip` incluido en Windows. Raudo no captura ni lee el contenido del portapapeles.

### Modo Mini

- Muestra una burbuja compacta, movible y siempre encima.
- Expone controles para cambiar al escritorio virtual izquierdo o derecho.
- Consulta las ventanas de otros escritorios únicamente al abrir el selector.
- Permite traer una ventana elegida al escritorio actual sin cerrarla.
- Recuerda su posición y puede ocultarse desde la propia burbuja o la bandeja.

<p align="center">
  <img src="docs/raudo-mini-dark.png" width="172" alt="Modo Mini de Raudo">
</p>

Para conservar la burbuja en todos los escritorios, abre `Win + Tab`, haz clic derecho sobre **Raudo Mini** y selecciona **Mostrar esta ventana en todos los escritorios**. Raudo delega este anclaje a Windows para evitar modificar la configuración del sistema mediante interfaces privadas.

La detección de ventanas utiliza la interfaz pública `IVirtualDesktopManager`. Windows restringe el movimiento de ventanas pertenecientes a otros procesos; esa operación emplea una capa de compatibilidad limitada por versión y se desactiva de forma segura si una actualización de Windows cambia el contrato interno. Raudo no instala controladores, servicios ni procesos auxiliares.

## Privacidad y red

Raudo no incluye telemetría, cuentas ni publicidad. Sólo se conecta a Internet cuando el usuario selecciona **Buscar actualizaciones**. Esa acción consulta la publicación oficial de GitHub y nunca descarga ni ejecuta archivos automáticamente.

Consulta [PRIVACY.md](PRIVACY.md) para conocer los datos locales y el comportamiento de red.

## Requisitos

- Windows 11 en una versión con soporte vigente.
- Windows 10 22H2 x64 es técnicamente compatible, aunque su soporte general terminó en octubre de 2025.
- .NET Framework 4.8.
- No requiere permisos de administrador.

Windows 7, Windows 8 y Windows 8.1 no forman parte del alcance compatible.

## Desarrollo

Compilar y ejecutar las pruebas unitarias:

```powershell
.\scripts\Build.ps1 -Test
```

Ejecutar también las pruebas de integración con las API de Windows:

```powershell
.\scripts\Build.ps1 -Test -IntegrationTest
```

Validar manualmente el movimiento entre dos escritorios virtuales:

```powershell
.\scripts\Build.ps1 -Test
.\artifacts\Raudo.Tests.exe --desktop-integration
```

Generar el ZIP portable y sus hashes:

```powershell
.\scripts\Package.ps1
```

El build local utiliza el compilador de .NET Framework incluido en Windows. `Raudo.sln` permite compilar el mismo código con Visual Studio o MSBuild.

## Uso responsable

Raudo está pensado para tareas locales autorizadas que necesitan evitar el reposo durante un periodo definido. No interactúa con aplicaciones de presencia, no ofrece modo oculto y no debe utilizarse para evadir políticas de una organización.

## Seguridad

Los reportes de vulnerabilidades deben enviarse de forma privada siguiendo [SECURITY.md](SECURITY.md). La procedencia y política prevista para binarios se documenta en [CODE_SIGNING.md](CODE_SIGNING.md).

## Licencia

[MIT](LICENSE). Consulta también [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
