# Contribuir

## Preparación

Raudo se desarrolla para .NET Framework 4.8 y Windows 10/11. No se requieren paquetes de ejecución externos.

```powershell
git clone https://github.com/adrielcrv/raudo-windows.git
cd raudo-windows
.\scripts\Build.ps1 -Test
```

## Cambios

- Mantén cada cambio limitado a un propósito verificable.
- Conserva el comportamiento local, visible y sin privilegios de administrador.
- No añadas telemetría, ejecución arbitraria, listeners de red ni descargas automáticas.
- Añade o actualiza pruebas cuando cambie el comportamiento.
- Ejecuta las pruebas de integración si modificas entrada, energía, sesión o APIs nativas.
- Ejecuta `Raudo.Tests.exe --desktop-integration` si modificas escritorios virtuales; la prueba requiere al menos dos escritorios y restaura el escritorio original.

Las propuestas se envían mediante pull request e incluyen su motivación, impacto y validación.
