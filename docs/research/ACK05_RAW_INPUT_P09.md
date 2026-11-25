# P0.9 · Atribuição de Raw Input no Windows

A sonda independente Ack05Enhanced.RawProbe recebe WM_INPUT em uma janela de diagnóstico em primeiro plano. O operador inicia o roteiro por clique. Perda de foco interrompe a etapa, descarta o estado reconstruído e exige novo clique para repeti-la. Os rótulos das etapas descrevem estímulos solicitados. A evidência de origem é o dispositivo resolvido no cabeçalho da mensagem.

## Identidade e captura

A sonda obtém o Device Path por GetRawInputDeviceInfo(RIDI_DEVICENAME), abre a interface em um conjunto de informações SetupAPI e consulta o Device Instance ID e o Container ID do devnode. Para atribuir ao ACK05 Bluetooth, cruza o Instance ID com a enumeração HID do projeto (atributos HID 28BD:0202 e capacidades da coleção) e verifica o serviço HID-over-GATT 1812 no identificador desta revisão. Interfaces Raw Input de mouse e teclado podem ter GUIDs de classe diferentes das interfaces HID do mesmo devnode. Duas coleções de mouse com o mesmo uso permanecem distintas. O handle é local à sessão. Chegadas e remoções invalidam a identidade e o conjunto de teclas associado.

O log JSONL local registra relógio do host com fuso, fase, eventos de ciclo de vida, identidade, dados brutos e estado das teclas por fonte. O horário representa o processamento da mensagem no host. MakeCode e VKey são números decimais no JSON. O conjunto held representa scan codes em hexadecimal com prefixo E0/E1 e usa VKey como alternativa quando MakeCode é zero. RAWMOUSE inclui flags de movimento, coordenadas, flags dos botões e deltas assinados das rodas. RAWHID conserva a separação entre relatórios do mesmo WM_INPUT. [Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/raw-input) · [RAWINPUTHEADER](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputheader) · [RAWKEYBOARD](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawkeyboard) · [SetupAPI](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/accessing-the-properties-of-installed-device-interface-classes)

## Inventário de 24/11/2026

Os três registros com RIDEV_DEVNOTIFY foram aceitos. O mesmo Container ID agrupou os quatro Instance IDs distintos. Os caminhos e identidades integrais estão no log privado `captures/private/p09-20261124-224256-708.jsonl`.

| Coleção | Uso | hDevice |
|---|---|---|
| Col01 · mouse | 0001:0002 | 0x6F01DD |
| Col02 · mouse | 0001:0002 | 0xB9901EF |
| Col03 · teclado | 0001:0006 | 0xB98071F |
| Col04 · digitalizador | 000D:0002 | 0x2304F7 |

## Interpretação

A primeira passagem teve 21 fases consecutivas entre 22:43:38 e 22:45:44. Col03 do ACK05 entregou 52 RAWKEYBOARD. Col01, Col02 e Col04 entregaram zero eventos. O teclado convencional Bluetooth 046D:B378, hDevice `0x307B4`, com Container ID distinto do ACK05, entregou 248 RAWKEYBOARD (incluindo repetições). Um mouse convencional 046D:B034, hDevice `0x1A00583`, entregou 128 RAWMOUSE. O contador inclui movimentos incidentais. Nenhum pacote chegou de origem desconhecida.

Na repetição convencional, Ctrl+O produziu `1D+ 18+ 18− 1D−` e Ctrl+S produziu `1D+ 1F+ 1F− 1D−` em 0x307B4. Na fase K4 houve `1D+ 1F+ 1D− 1F−` em 0xB98071F. Isso distingue a origem física de Ctrl+S mesmo com sobreposição semântica. Os números são MakeCode em hexadecimal, + é pressão e − é soltura. Durante as fases convencionais apareceram também Ctrl+Z, + e − numéricos; alguns limites de fase cortaram sequências. O operador informou ter perdido etapas.

A versão final oferece uma prévia e aguarda um clique para cada etapa. Argumentos numéricos opcionais selecionam as posições do roteiro original (1 a 21) para uma repetição isolada. As amostras e marcações das passagens são preservadas em logs distintos.

## Repetição isolada e mapeamento observado

A segunda passagem terminou às 22:48:21, com fechamento da janela às 22:48:25. O log privado `captures/private/p09-20261124-224613-503.jsonl` contém 14 eventos do teclado ACK05, quatro do teclado convencional e 590 do mouse convencional. As duas coleções de mouse e o digitalizador do ACK05 permaneceram silenciosos. A soma das duas passagens foi de 66 eventos atribuídos ao ACK05, todos de Col03.

| Estímulo | Make/break em Col03, hexadecimal | Interpretação | Passagem |
|---|---|---|---|
| K1 | 1D+ 18+ 1D− 18− | Ctrl+O | isolada |
| K2 | 1D+ 31+ 1D− 31− | Ctrl+N | inicial |
| K3 | 3F+ 3F− | F5 | inicial |
| K4 | 2A+ 2A− | Shift esquerdo | isolada |
| K5 | 1D+ 1D− | Ctrl esquerdo | isolada |
| K6 | 38+ 38− | Alt esquerdo | isolada |
| K7 | 1D+ 1F+ 1D− 1F− | Ctrl+S | isolada |
| K8 | 1D+ 2C+ 1D− 2C− | Ctrl+Z | inicial |
| K9 | 39+ 39− | Espaço | inicial |
| K10 | 1D+ 2A+ 2C+ 1D− 2A− 2C− | Ctrl+Shift+Z | inicial |
| Dial horário | 1D+ 4E+ 1D− 4E− | Ctrl e adição numérica | inicial, três ciclos |
| Dial anti-horário | 1D+ 4A+ 1D− 4A− | Ctrl e subtração numérica | inicial, dois ciclos na fase |
| K11 breve | nenhum evento do ACK05 | sem assinatura atribuível | isolada |

A primeira janela de K11 continha outro ciclo idêntico ao giro anti-horário, junto à fronteira entre etapas. A repetição isolada não produziu esse ciclo. A contagem de ciclos não estabelece quantos detentes o operador percorreu.

O mesmo Ctrl+O entrou pelo teclado convencional às 22:45:34.577 e pelo ACK05 às 22:47:26.181. Ctrl+S entrou pelo convencional às 22:45:39.223 e pelo ACK05 às 22:48:06.701. Além dos handles distintos, diferiram VID/PID, Device Path, Instance ID e Container ID. Os horários indicam o primeiro make de cada sequência. O resultado valida atribuição para esta unidade nesta sessão. A estabilidade dos handles após reconexão e a supressão de atalhos do sistema não foram ensaiadas.

## Confirmação final

A terceira passagem manual (`p09-20261124-224908-285.jsonl`) reproduziu individualmente as sequências da tabela para K2, K3, K8, K9, K10 e giro anti-horário. O giro horário gerou apenas Ctrl nessa tentativa, que o operador apontou como incerta. A quarta passagem (`p09-20261124-225046-414.jsonl`) isolou o sentido horário: três ciclos completos de Ctrl e adição numérica em Col03. O operador confirmou o sentido e informou ter provavelmente percorrido mais de um detente. Assim, todas as correspondências da tabela tiveram uma janela de confirmação manual. K11 permaneceu sem assinatura na sua janela isolada.

As quatro passagens somam 106 RAWKEYBOARD do ACK05, 252 RAWKEYBOARD do teclado de referência e 1.719 RAWMOUSE do mouse convencional. O ACK05 entregou zero RAWMOUSE e zero RAWHID. Nenhum input foi registrado fora das janelas de captura em primeiro plano. Os quatro logs ficam em `captures/private/`.

A referência macOS identifica controles em relatórios de teclado de oito bytes. No Windows, RAWKEYBOARD já representa a tradução da pilha de teclado. Atribuição depende do hDevice e da identidade da coleção, inclusive para Shift, Ctrl e Alt isolados. Correspondência por atalho ou proximidade temporal é insuficiente. Nenhum código da referência comunitária foi incorporado. [Investigação macOS](https://github.com/MartinSadovy/xppen-ack05/blob/598252d30c86d1ac62ed4b325f1485acf629e5e1/docs/superpowers/specs/2026-04-16-ack05-macos-driver-design.md) · [Parser de referência](https://github.com/MartinSadovy/xppen-ack05/blob/598252d30c86d1ac62ed4b325f1485acf629e5e1/Sources/ACK05Core/Device/KeyboardReportParser.swift)

Raw Input permite atribuir mensagens entregues ao consumidor. A API não oferece veto global por hDevice. RIDEV_NOLEGACY afetaria as mensagens legadas do processo por uso. Hooks de baixo nível carecem do identificador físico. Uma correlação heurística pode consumir teclas de outro teclado. A supressão seletiva permanece fora do diagnóstico e sem solução demonstrada dentro destas restrições. [RAWINPUTDEVICE](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputdevice) · [KBDLLHOOKSTRUCT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct)

Build Release da solução com zero avisos e zero erros. SelfTest completo com 60/60 aprovados. O novo teste cobre isolamento por origem, soltura, E0, limpeza por remoção, origem nula, roda horizontal com sinal e enquadramento de RAWHID.
