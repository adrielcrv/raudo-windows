# Seguridad

## Versiones compatibles

La versión estable más reciente recibe correcciones de seguridad. Las compilaciones locales y los artefactos de workflows no se consideran versiones distribuidas.

## Reportar una vulnerabilidad

No publiques vulnerabilidades como issues. Utiliza el reporte privado de seguridad del repositorio:

https://github.com/adrielcrv/raudo-windows/security/advisories/new

Incluye la versión afectada, el impacto, los pasos de reproducción y cualquier mitigación conocida. Se confirmará la recepción cuando el reporte haya sido revisado.

## Límites de seguridad

Raudo no requiere administrador, no instala servicios, no escucha conexiones y no ejecuta comandos configurables. El actualizador sólo opera después de una confirmación, acepta el asset versionado del repositorio oficial, exige sumas SHA-256 válidas y restringe el reemplazo a la instalación local de Raudo. Las funciones nuevas deben preservar estos límites salvo que una revisión pública documente y justifique el cambio.
