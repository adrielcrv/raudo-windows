# Privacidad

Raudo funciona localmente y no recopila telemetría, identificadores, contenido del usuario ni estadísticas de uso.

## Datos locales

La duración seleccionada se guarda en:

```text
%LOCALAPPDATA%\Raudo\settings.json
```

El mismo archivo conserva si Modo Mini está activo, su posición, la posición elegida para Salto, uno de sus tres niveles de opacidad, si ya se mostró la guía de escritorios y el vencimiento UTC de una sesión activa de Pulso. No contiene consultas, resultados, títulos de ventanas, nombres de aplicaciones, audio o texto reconocido.

La gramática fija utilizada por la función de voz se prepara en:

```text
%LOCALAPPDATA%\Raudo\voice\commands-es-MX.srgs.xml
```

Este archivo contiene únicamente las órdenes incluidas con Raudo. No incorpora frases pronunciadas, grabaciones, consultas o nombres de aplicaciones instaladas.

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

Raudo no lee el contenido de las ventanas, no recorre ni indexa archivos, no conserva el texto de búsqueda y no envía estos datos por la red. El catálogo de aplicaciones se prepara bajo demanda como máximo una vez por proceso; el indicador de preparación se muestra sólo durante esa operación y no registra su contenido. Las carpetas remotas y rutas de red no se consultan ni se muestran como accesos directos.

## Órdenes de voz

Raudo solicita acceso al micrófono únicamente después de que el usuario presiona `Ctrl + Alt + V` o selecciona **Hablar con Raudo**. El reconocimiento utiliza las restricciones de gramática de Windows y un catálogo local de acciones; no activa dictado libre, búsqueda web del reconocedor o escucha mediante palabra de activación.

Windows entrega a Raudo el texto reconocido durante esa sesión. Raudo lo mantiene en memoria el tiempo necesario para resolver la acción y mostrar su confirmación; no guarda audio, texto, confianza, historial o estadísticas de voz. Si la primera orden no se entiende o no coincide con el catálogo seguro, puede realizar un segundo intento dentro de la misma sesión y después libera el reconocedor y el micrófono.

Los nombres y alias de aplicaciones se preparan localmente para reconocer acciones como **abre Bloc de notas**. No se escriben en la gramática persistente ni se envían por la red.

## Controles multimedia

En el modo automático, las acciones multimedia envían únicamente uno de seis comandos fijos de reproducción o volumen documentados por Windows después de una selección explícita. El texto de búsqueda no puede convertirse en una tecla o comando arbitrario.

Al abrir las opciones multimedia de Mini, Raudo solicita a Windows la lista de sesiones disponibles. Para permitir elegir un reproductor, utiliza únicamente el identificador de la aplicación, el estado de reproducción y las capacidades de control declaradas por esa sesión. Esta información permanece en memoria y se actualiza al volver a abrir el menú; no se almacena ni se envía por la red.

Raudo no solicita títulos, contenido, direcciones, progreso, carátulas o historial. Tampoco inspecciona pestañas, ventanas, procesos o audio para identificar el reproductor. La selección no mantiene sondeo, temporizadores ni conexiones de red y vuelve al comando multimedia estándar cuando el usuario elige **Automático de Windows**.

## Conexiones de red

Raudo no realiza conexiones automáticas. Al seleccionar **Buscar actualizaciones**, envía una solicitud HTTPS a la API pública de GitHub para consultar la última publicación de `adrielcrv/raudo-windows`.

La respuesta de metadatos se limita a 64 KB. Si existe una versión nueva y el usuario confirma la instalación, Raudo descarga únicamente el asset esperado desde la API oficial de GitHub, limita su tamaño, valida la versión y verifica tanto la suma SHA-256 del paquete como la del ejecutable. Una copia portable sólo puede abrir la publicación oficial en el navegador.

El tratamiento de datos de esa solicitud está sujeto a la política de privacidad de GitHub.

Las órdenes **abre YouTube** y **muestra el clima** abren una dirección o búsqueda explícita en el navegador predeterminado. La conexión posterior pertenece al navegador y a los sitios que el usuario abre; Raudo no recibe ni conserva su contenido.
