# Privacidad

Raudo funciona localmente y no recopila telemetría, identificadores, contenido del usuario ni estadísticas de uso.

## Datos locales

La duración seleccionada se guarda en:

```text
%LOCALAPPDATA%\Raudo\settings.json
```

La opción **Iniciar con Windows** crea o elimina el valor `Raudo` en:

```text
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```

Durante una actualización confirmada, los archivos temporales se guardan en:

```text
%LOCALAPPDATA%\Raudo\Updates
```

La copia anterior del ejecutable se conserva únicamente durante el reinicio y después se elimina.

## Datos transitorios de Salto

Cuando Salto se abre, Raudo consulta los títulos de las ventanas visibles, el catálogo de aplicaciones registrado por Windows y un conjunto limitado de carpetas conocidas locales. Los nombres, identificadores y la consulta escrita se conservan únicamente en memoria durante la sesión necesaria para mostrar y filtrar resultados.

Los cálculos y conversiones se procesan localmente. Raudo sólo escribe el resultado en el portapapeles después de que el usuario selecciona la acción correspondiente; no lee ni conserva el contenido anterior.

Raudo no lee el contenido de las ventanas, no recorre ni indexa archivos, no conserva el texto de búsqueda y no envía estos datos por la red. El catálogo de aplicaciones se prepara bajo demanda como máximo una vez por proceso. Las carpetas remotas y rutas de red no se consultan ni se muestran como accesos directos.

## Conexiones de red

Raudo no realiza conexiones automáticas. Al seleccionar **Buscar actualizaciones**, envía una solicitud HTTPS a la API pública de GitHub para consultar la última publicación de `adrielcrv/raudo-windows`.

La respuesta de metadatos se limita a 64 KB. Si existe una versión nueva y el usuario confirma la instalación, Raudo descarga únicamente el asset esperado desde la API oficial de GitHub, limita su tamaño, valida la versión y verifica tanto la suma SHA-256 del paquete como la del ejecutable. Una copia portable sólo puede abrir la publicación oficial en el navegador.

El tratamiento de datos de esa solicitud está sujeto a la política de privacidad de GitHub.
