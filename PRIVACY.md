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

No se escriben otras ubicaciones durante el uso normal.

## Conexiones de red

Raudo no realiza conexiones automáticas. Al seleccionar **Buscar actualizaciones**, envía una solicitud HTTPS a la API pública de GitHub para consultar la última publicación de `adrielcrv/raudo-windows`.

La respuesta se limita a 64 KB, se valida que la página de publicación pertenezca a `github.com` y sólo puede abrirse en el navegador después de una confirmación del usuario. Raudo no descarga ni ejecuta actualizaciones.

El tratamiento de datos de esa solicitud está sujeto a la política de privacidad de GitHub.
