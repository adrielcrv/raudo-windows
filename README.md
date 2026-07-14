<p align="center">
  <img src="assets/brand/raudo-mark.svg" width="72" height="72" alt="Raudo">
</p>

# Raudo

Raudo reúne utilidades locales y ligeras para Windows en una aplicación de bandeja. Está diseñado para permanecer disponible sin mantener trabajo innecesario en segundo plano.

[![Build](https://github.com/adrielcrv/raudo-windows/actions/workflows/build.yml/badge.svg)](https://github.com/adrielcrv/raudo-windows/actions/workflows/build.yml)
[![CodeQL](https://github.com/adrielcrv/raudo-windows/actions/workflows/codeql.yml/badge.svg)](https://github.com/adrielcrv/raudo-windows/actions/workflows/codeql.yml)

<p align="center">
  <img src="docs/raudo-dark.png" width="520" alt="Interfaz de Raudo en modo oscuro">
</p>

## Funciones

### Pulso

- Evita temporalmente que el sistema y la pantalla entren en reposo.
- Genera una entrada mínima únicamente después de 45 segundos sin actividad.
- Restaura el cursor a su posición original.
- Se limita a 15, 30, 60 o 120 minutos.
- Se detiene al bloquear, suspender o cerrar la sesión interactiva de Windows.
- No se activa automáticamente al iniciar la aplicación.

### Salto

- Abre un lanzador local con `Ctrl + Alt + Espacio` o desde la bandeja.
- Busca acciones sin distinguir mayúsculas ni acentos.
- Permite iniciar o detener Pulso, abrir Recortes y Raudo, controlar Modo Mini y cambiar entre escritorios disponibles.
- Se opera con flechas, `Enter`, `Escape` o un clic.
- Se crea únicamente al primer uso y no mantiene historial, indexación, scripts ni procesos auxiliares.

<p align="center">
  <img src="docs/raudo-salto-dark.png" width="640" alt="Salto de Raudo en modo oscuro">
</p>

### Recortar pantalla

Abre la Herramienta Recortes mediante el protocolo `ms-screenclip` incluido en Windows. Raudo no captura ni lee el contenido del portapapeles.

### Modo Mini

- Se recoge como un control de borde compacto que permanece visible sin invadir el contenido.
- Se revela y se recoge con una transición breve que respeta la preferencia de animaciones de Windows.
- Al minimizar la ventana principal, una transición conectada confirma visualmente su llegada a Mini.
- Sigue automáticamente al escritorio activo y permanece siempre encima.
- Reduce su presencia durante pantalla completa y presentaciones, sin perder el área de interacción.
- Muestra únicamente las direcciones de escritorio que están disponibles.
- Consulta las ventanas de otros escritorios únicamente al abrir el selector.
- Permite traer una ventana elegida al escritorio actual sin cerrarla.
- Recuerda el borde y la altura elegidos; puede ocultarse desde la propia pestaña o la bandeja.
- Indica el estado de la sesión sin alterar el azul de marca y recuerda los umbrales de 15 y 5 minutos.

<p align="center">
  <img src="docs/raudo-mini-dark.png" width="144" alt="Modo Mini de Raudo">
</p>

Raudo solicita a Windows que muestre únicamente su pestaña Mini en todos los escritorios. No modifica la configuración global de Vista de tareas. Si una versión de Windows no permite el anclaje automático, Raudo conserva como alternativa la configuración manual mediante `Win + Tab`.

La detección de ventanas utiliza la interfaz pública `IVirtualDesktopManager`. El seguimiento de la pestaña, los límites de navegación y el movimiento de ventanas entre procesos emplean una capa de compatibilidad limitada por versión; esas funciones se desactivan de forma segura si una actualización de Windows cambia el contrato interno. Raudo no instala controladores, servicios ni procesos auxiliares.

## Privacidad y red

Raudo no incluye telemetría, cuentas ni publicidad. Las búsquedas de Salto permanecen en memoria y se utilizan únicamente para filtrar el catálogo local. Raudo sólo se conecta a Internet cuando el usuario selecciona **Buscar actualizaciones**. En una instalación local, puede descargar el paquete oficial después de una confirmación, validar su versión y sus sumas SHA-256, reemplazar el ejecutable de forma atómica y reiniciarse. Una copia portable abre la publicación para actualización manual.

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
