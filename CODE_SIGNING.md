# Política de firma de código

## Estado

Los candidatos de publicación se compilan en GitHub Actions, incluyen hashes SHA-256 y generan una atestación de procedencia. Un artefacto de workflow no se presenta como versión estable.

Los ejecutables públicos actuales no tienen firma Authenticode. Windows puede mostrar una advertencia de reputación para una descarga nueva; esto no se presenta como garantía de seguridad ni como detección de malware. Antes de publicar, la versión estable se reconstruye desde el commit integrado, conserva la suma SHA-256 generada por el workflow y se acompaña de una atestación verificable de GitHub.

La ruta prevista para Authenticode es solicitar un programa de firma para proyectos de código abierto. No se afirmará que un binario está firmado hasta verificar su cadena, identidad y timestamp en el asset publicado.

## Procedencia

- Repositorio: `https://github.com/adrielcrv/raudo-windows`
- Responsable del código y mantenimiento: `Adrielcrv`
- Aprobador de publicaciones: `Adrielcrv`
- Sistema de build confiable: GitHub Actions
- Licencia: MIT

Cada publicación requiere aprobación manual. Los workflows no publican versiones estables automáticamente.

## Privacidad

Raudo no transfiere información a otros sistemas salvo cuando el usuario solicita explícitamente comprobar o instalar una actualización. El comportamiento completo se describe en [PRIVACY.md](PRIVACY.md).
