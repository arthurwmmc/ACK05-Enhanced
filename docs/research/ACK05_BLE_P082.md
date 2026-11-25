# ACK05 no Windows · P0.8.2

## Ensaio de despertar

Em 24/11/2026, com horários de São Paulo, a sonda existente foi executada com `--capture 180`. A janela de três minutos acomodou o tempo de resposta pelo chat e a observação de pelo menos um minuto após K1. A descoberta pelo nome verificou o identificador PnP 28BD:0202. A sonda verificou a ausência de XPPenTablet antes de abrir a sessão.

| Horário | Observação |
|---|---|
| 22:32:07.232 | FFE0 em SharedReadOnly, Bluetooth Connected e GattSession Active. |
| 22:32:07.255 | Ambos os CCCDs lidos como Notify. Ouvintes ValueChanged registrados em 0002 e depois em 0003. |
| 22:32:18.185 | Aos 10,93 s, o log continuava sem transição desde Connected/Active. Em seguida foi enviado o sinal ao operador. O estado neste ponto foi acompanhado por eventos, sem uma segunda consulta ao adaptador. |
| 22:32 | O operador informou o minuto da pressão breve de K1. A confirmação já havia chegado às 22:33:01.429. O relógio do host não mediu o instante mecânico. |
| 22:35:07.280 | Fim da observação, Connected/Active. Uma sessão, zero desconexões, zero pacotes em 0002 e em 0003. |

A janela cobriu mais de 125 segundos após a confirmação. Nenhum pacote hexadecimal, heartbeat 02 F2, reconhecimento 02 F8 ou relatório 02 F0 foi recebido. O despertar por K1 foi insuficiente para reproduzir os 11 heartbeats do P0.7. O estado Notify lido e a conexão ativa coexistiram com ausência de callbacks. O ensaio não permite localizar a causa no periférico ou no Windows. A sonda terminou com código 0 e descartou os ouvintes e a sessão. Log privado local em `captures/private/p082-20261124-223202.log`.

## Caminho HID de teclado

A especificação inicial de macOS considerava uma coleção proprietária FF0A:0001. O adendo do ensaio no mesmo documento encontrou quatro coleções HID no Bluetooth, duas de mouse (IDs 09 e 01), teclado (06) e caneta (07). O código efetivamente utilizado seleciona o teclado por VID/PID e uso 0001:0006. Essa distinção diz respeito ao descritor HID. O serviço GATT FFE0 tem uma interface independente. [Especificação e resultados](https://github.com/MartinSadovy/xppen-ack05/blob/598252d30c86d1ac62ed4b325f1485acf629e5e1/docs/superpowers/specs/2026-04-16-ack05-macos-driver-design.md) · [Seleção da coleção](https://github.com/MartinSadovy/xppen-ack05/blob/598252d30c86d1ac62ed4b325f1485acf629e5e1/Sources/ACK05Core/Device/KeyboardInputDevice.swift)

Na representação entregue ao parser macOS, os oito bytes contêm ID 06, máscara de modificadores e seis posições de usos de teclado. A combinação de modificador e uso identifica o controle. A diferença entre estados sucessivos fornece pressão e soltura. Cada observação de rotação produz um pulso. Estes são candidatos para validação individual no Windows. [Parser da referência](https://github.com/MartinSadovy/xppen-ack05/blob/598252d30c86d1ac62ed4b325f1485acf629e5e1/Sources/ACK05Core/Device/KeyboardReportParser.swift)

| Controle | Modificador / uso (hex) | Assinatura |
|---|---|---|
| K1 | 01 / 12 | Ctrl+O |
| K2 | 01 / 11 | Ctrl+N |
| K3 | 00 / 3E | F5 |
| K4 | 02 / 00 | Shift sozinho |
| K5 | 01 / 00 | Ctrl sozinho |
| K6 | 04 / 00 | Alt sozinho |
| K7 | 01 / 16 | Ctrl+S |
| K8 | 01 / 1D | Ctrl+Z |
| K9 | 00 / 2C | Espaço |
| K10 | 03 / 1D | Ctrl+Shift+Z |
| Dial horário | 01 / 57 | Ctrl e adição do teclado numérico |
| Dial anti-horário | 01 / 56 | Ctrl e subtração do teclado numérico |

K11 curto ficou sem assinatura HID distinguível no projeto de referência. O clique identificado no USB do projeto Windows não valida o mesmo controle em Bluetooth. A tabela descreve observações de protocolo. O projeto consultado usa PolyForm Noncommercial 1.0.0 e nenhum código dele foi incorporado. [Limitação de K11](https://github.com/MartinSadovy/xppen-ack05/tree/598252d30c86d1ac62ed4b325f1485acf629e5e1#readme) · [Licença](https://github.com/MartinSadovy/xppen-ack05/blob/598252d30c86d1ac62ed4b325f1485acf629e5e1/LICENSE)

## Acesso e supressão no Windows

SetupAPI/HidP permite inventariar as coleções e suas capacidades. O Windows reserva o acesso de leitura a coleções de teclado e mouse para sua pilha de entrada. O ensaio anterior desta unidade também encontrou recusa de leitura direta nas quatro coleções. Naquele ensaio, com o software oficial fechado, K1, K2 e dial não produziram eventos Raw Input atribuíveis ao ACK05. [Registro do projeto](ACK05_PROTOCOL.md#windows-direct-bluetooth-observation-2026-07-16) · [Arquitetura HID do Windows](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/hid-architecture)

Em uma próxima sonda, resolver hDevice de WM_INPUT para uma coleção identificada como Bluetooth do ACK05 e descartar fontes ambíguas. Para RIM_TYPEKEYBOARD, RAWKEYBOARD fornece make/break e scan codes do Windows. Reconstruir modificadores por dispositivo é necessário antes de comparar assinaturas. Aplicar diretamente o parser de oito bytes a essa estrutura geraria resultados incorretos. [Estruturas de Raw Input](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinput) · [Uso de Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/using-raw-input)

RIDEV_NOLEGACY afeta mensagens legadas da aplicação registrada por página de uso e uso. Um hook de teclado de baixo nível pode suprimir uma tecla, mas o evento recebido carece do identificador do teclado físico. Correlacionar o hook com WM_INPUT por horário pode consumir o Ctrl de outro teclado. Sob a restrição de preservar outros teclados e os drivers, a proposta para HID é apenas observar eventos atribuídos. Os atalhos originais ainda podem alcançar a aplicação em foco. [Registro por uso](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputdevice) · [Campos do hook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct)

## Inventário local e decisão

Após o processo GATT terminar, a sonda Windows executou primeiro `list` e depois `raw-input --list`. SetupAPI/HID retornou quatro coleções 28BD:0202: dois mouses 0001:0002, teclado 0001:0006 e digitalizador 000D:0002. Os respectivos comprimentos de entrada foram 8, 8, 8 e 10 bytes, todos com saída de zero bytes. Raw Input listou dois mouses, um teclado e um HID genérico. A enumeração não testa entrega de teclas. Os IDs de relatório citados acima provêm da referência macOS. Os caminhos completos estão nos logs privados locais `p082-hid-list.log` e `p082-raw-list.log`.

| Capacidade | Teclado HID | FFE0 proprietário |
|---|---|---|
| K1–K10 e giro | Assinaturas conhecidas no macOS. Atribuição física Windows pendente. | Buscar 02 F0 em 0003. Mapeamento físico Windows pendente. |
| Simultaneidade e K11 | Acordes podem ser ambíguos. Clique sem assinatura conhecida. | Um relatório de estado pode distinguir controles simultâneos. Layout e clique dependem de validação nesta revisão. |
| Bateria | Sem campo no relatório de teclado estudado. | 02 F2 observado no P0.7. |
| Atalhos nativos | Podem chegar ao aplicativo em foco. | Efeito do modo proprietário sobre os atalhos nativos ainda desconhecido. |

**Prioridade sugerida:** testar primeiro a atribuição HID com uma sonda passiva em janela de teste, uma tecla por vez, incluindo controle negativo com outro teclado. Se a fonte for nula, ambígua ou silenciosa, registrar a limitação e encerrar esse caminho de remapeamento. Para FFE0, a próxima investigação é demonstrar entrega reproduzível de notificações antes de avaliar qualquer ativação. A abertura SharedReadOnly por si só não explicou a diferença entre P0.7 e as duas tentativas passivas seguintes.

Para a arquitetura futura, manter no projeto Windows um adaptador Raw Input que reconstrua estado por fonte e outro adaptador WinRT que valide e classifique pacotes de FFE0. Selecionar um único produtor por sessão e descartar callbacks atrasados pela identidade da sessão. Encaminhar estados físicos validados ao processamento comum do Core, preservando o cancelamento P0 antes da limpeza, e publicar bateria separadamente. A App selecionaria o transporte com Output OFF inicial. O diagnóstico atual termina no log, sem ligação com execução de ações.
