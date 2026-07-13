# Política de firma de código

## Estado

Los candidatos de publicación se compilan en GitHub Actions, incluyen hashes SHA-256 y generan una atestación de procedencia. Un artefacto de workflow no se presenta como versión estable.

La primera versión estable permanecerá como borrador hasta contar con una ruta de firma de código verificable. La opción prevista es solicitar el programa gratuito de SignPath Foundation para proyectos de código abierto. La aceptación no está garantizada y no se afirmará que un binario está firmado hasta verificar su cadena y timestamp.

## Procedencia

- Repositorio: `https://github.com/adrielcrv/raudo-windows`
- Responsable del código y mantenimiento: `Adrielcrv`
- Aprobador de publicaciones: `Adrielcrv`
- Sistema de build confiable: GitHub Actions
- Licencia: MIT

Cada publicación requiere aprobación manual. Los workflows no publican versiones estables automáticamente.

## Privacidad

Raudo no transfiere información a otros sistemas salvo cuando el usuario solicita explícitamente comprobar actualizaciones. El comportamiento completo se describe en [PRIVACY.md](PRIVACY.md).
