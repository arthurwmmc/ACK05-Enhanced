# ACK05 direto por Bluetooth LE no Windows

## P0.7 · Captura física de notificações

Em 24/11/2026 às 22h13 (São Paulo), o probe redescobriu a unidade por nome com
XPPenTablet encerrado, abriu FFE0 em SharedReadOnly, leu Notify nos dois CCCDs
e instalou ValueChanged em 0002 e depois em 0003. Em 60 segundos, recebeu
zero pacotes em 0002 e onze em 0003. Todos continham os mesmos dez bytes
`02 F2 01 50 00 00 00 00 00 00`, compatíveis com bateria a 80% e flag de
carga zero. Os dez intervalos entre callbacks ficaram entre 4,945 e 5,420 s.
Houve uma sessão contínua. F0, F8 e reconexão física seguem sem evidência
neste ensaio. Nenhum comando ou escrita de CCCD foi necessário.

Para repetir, usar `dotnet run --project src/Ack05Enhanced.BleProbe -c Release -- --capture 60`.
A janela começa após a instalação do segundo ouvinte. Cada linha de pacote
contém UTC, sequência, UUID, tamanho, hexadecimal completo, intervalo monotônico
e estado de conexão. O build Release passou e a suíte chegou a 59/59 testes.
Os resultados abaixo documentam a etapa anterior de investigação.

## Resultado de bancada

O Windows conseguiu identificar o ACK05 e enumerar o serviço de controle real por GATT. A leitura do fluxo de botões pelo aplicativo ainda depende de obter acesso às características desse serviço. Nesta unidade, a abertura de FFE0 em modo compartilhado de leitura retornou `SharingViolation`. O resultado justifica manter o transporte de produção fora da aplicação nesta etapa.

| Verificação | Resultado no equipamento |
| --- | --- |
| Host e descoberta | Windows build 26300. Um endpoint BLE elegível, nome **Shortcut Remote**, já conectado pelo Windows. A consulta GATT sem cache confirmou a comunicação. O ensaio cobriu acesso a uma conexão existente. |
| Identidade | O PnP ID em Device Information declarou origem USB (2), fabricante **28BD** e produto **0202**. Essa combinação identifica o modelo mesmo quando o meio de transporte é BLE. |
| Serviços enumerados | **1800** Generic Access, **1801** Generic Attribute, **180A** Device Information, **1812** HID, **FFE0** de controle e **00010203-0405-0607-0809-0a0b0c0d1912**. |
| Revisão | O último UUID é o marcador OTA associado à família **Telink / ACK05-B** nas fontes. É evidência de família, sem confirmação do modelo exato do chip ou da versão de firmware. Só o UUID do serviço OTA foi observado. |
| Acesso a FFE0 | A enumeração implícita inicial retornou `AccessDenied`. A abertura explícita com `GattSharingMode.SharedReadOnly` retornou `SharingViolation`, inclusive na repetição com o binário Release final. O status informa conflito de compartilhamento. O processo ou componente que detém o serviço não foi identificado. |
| Notificações, bateria e atividade | **0 notificações recebidas**. O conflito impediu ler os CCCDs e instalar ouvintes. Não houve amostra física de F0, F2 ou F8. O serviço padrão de bateria **180F** também não apareceu. A porcentagem de carga permanece desconhecida. |
| Reconexão e endereço | A unidade permaneceu conectada nas janelas de 45 e 20 segundos. As reaberturas do primeiro ensaio viram o mesmo endereço informado pelo Windows. Não ocorreu um ciclo físico de suspensão/desligamento que permitisse medir rotação. Uma nova abertura de handle, por si só, não caracteriza reconexão física. |

O diagnóstico final terminou com código **3**, uma tentativa de inventário concluída, conjunto de características FFE0 sem verificação e zero notificações. Esses resultados não demonstram ausência de tráfego entre o controle e outro cliente. O ensaio não realizou pairing, escrita em característica ou descritor, alteração de driver, configuração do sistema ou saída sintética. O endereço e o ID privado do endpoint são omitidos do relatório.

## Protocolo segundo as fontes

Os UUIDs curtos abaixo usam a base Bluetooth `xxxxxxxx-0000-1000-8000-00805f9b34fb`. Por exemplo, o serviço expande para `0000ffe0-0000-1000-8000-00805f9b34fb` e a característica 0003 para `00000003-0000-1000-8000-00805f9b34fb`.

| Dentro de FFE0 | Função documentada | Evidência desta unidade |
| --- | --- | --- |
| 0001 | Canal de comandos do host | Função conhecida pelas implementações externas. Propriedades locais inacessíveis. |
| 0002 | Notificações de status e reconhecimento | Nas unidades documentadas, a habilitação deste canal participa da disponibilização do fluxo em 0003. Estado local do descritor desconhecido. |
| 0003 | Notificações do controle. O prefixo 02 F0 indica estado físico, 02 F2 indica bateria e 02 F8 sinaliza retomada/reconexão no protocolo. | Papéis documentados externamente. Nenhum pacote local recebido. |

O driver de Romain Gehrig documenta esses UUIDs completos no backend BlueZ e reporta rotação de endereço ao despertar. Seu fluxo Linux inclui procedimentos de vínculo que não integram o diagnóstico Windows. O projeto Windows original pesquisou principalmente as coleções HID. Encontrar apenas coleções HID padrão naquela pesquisa é compatível com a existência de um serviço proprietário acessível por uma API GATT distinta. [Backend Linux](https://github.com/RomainGehrig/XPPen-ACK05-driver/blob/c978e9ae0b5ea61db076713dfcbe1cfdd87309da/src/xppen_ack05_driver/bluetooth.py) · [Pesquisa Windows original](https://github.com/46slv/ACK05-Enhanced/blob/a41b88ffa30837c657e61f84cb41977d9285669d/docs/research/ACK05_PROTOCOL.md)

No macOS, Jordi Wippert documentou um exemplar Telink com endereço novo após religamento. O tipo de escrita e a chegada do primeiro heartbeat influenciaram o início do modo proprietário. Um reconhecimento de comando isolado não comprovou a mudança de modo. O autor também aponta evidência física de outra revisão com Nordic nRF52833. A ausência do marcador Telink deve produzir **revisão desconhecida**. Uma identificação positiva de Nordic exige evidência adicional. [Relato técnico](https://jordiwippert.nl/posts/2026-09-02-xppen-ack05-macos-driver/) · [Protocolo do ack05d](https://github.com/LiveNL/ack05d/blob/b1f1126051e0454e38496f98c743a4ba41afbfc9/docs/PROTOCOL.md)

O classificador de diagnóstico usa os índices do decodificador Swift para F2. Em um pacote com pelo menos cinco bytes, o índice 3 contém a porcentagem e o índice 4 o indicador de carga. Percentuais fora de 0–100 e quadros curtos ficam sem classificação. F0 e F8 produzem mensagens de atividade, sem conversão em ações. Os índices foram testados com dados simulados. [Decodificador de referência](https://github.com/LiveNL/ack05d/blob/b1f1126051e0454e38496f98c743a4ba41afbfc9/Sources/ack05d/FrameDecoder.swift)

## Implementação e reprodução

O executável separado `Ack05Enhanced.BleProbe` usa .NET 8 e a projeção oficial WinRT com alvo Windows 10 build 19041. Uma consulta sem cache solicita a conexão quando o Windows disponibiliza um endpoint elegível. A descoberta usa um `DeviceWatcher` por nome, aceita as grafias com e sem espaço e prefere um único endpoint já conectado. Havendo ambiguidade, aguarda nova descoberta. O programa compara endereços apenas em memória. Um endereço diferente é registrado como mudança entre candidatos, sem atribuir identidade física entre duas unidades de mesmo nome.

O inventário lista UUIDs de serviços. Para os valores padrão, só consulta PnP ID em 180A e Battery Level em 180F quando disponíveis e legíveis. Dentro de FFE0, enumera propriedades e verifica os três UUIDs. Em 0002/0003, consulta o descritor de configuração do cliente (**CCCD**, 2902) somente se houver Notify ou Indicate. Registra um ouvinte local somente quando o descritor já está habilitado. O Windows normalmente requer uma escrita no CCCD para iniciar uma assinatura. Esse estado fica a cargo da conexão existente e uma assinatura de outro cliente não garante entrega para este processo. [Cliente GATT no Windows](https://learn.microsoft.com/en-us/windows/uwp/devices-sensors/gatt-client) · [Modos de compartilhamento](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattsharingmode) · [Resultados de abertura](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattopenstatus)

Executar a partir da raiz do repositório:

```powershell
dotnet run --project .\src\Ack05Enhanced.BleProbe -c Release -- --seconds 45
```

A duração inclui descoberta e inventário. Ctrl+C encerra a observação. O código 0 indica que as três características foram enumeradas em pelo menos uma sessão, mesmo com zero notificações. O código 2 indica argumentos inválidos. O código 3 indica diagnóstico incompleto ou erro. A desconexão cancela as operações daquela sessão, remove ouvintes e inicia nova busca por nome. O probe não aciona a GUI, o runtime USB, o roteador ou SendInput.

## Arquitetura e próximos ensaios

1. **Acesso compartilhado.** Em uma rodada autorizada, repetir a abertura somente leitura em um ambiente onde FFE0 esteja disponível. Registrar propriedades reais de 0001/0002/0003, estados dos CCCDs, status GATT e contagem de notificações por canal. Investigar o conflito separadamente, sem tomar posse exclusiva do dispositivo.
2. **Identidade e reconexão.** Acompanhar uma suspensão e um religamento controlados. Correlacionar PnP ID, assinatura FFE0 e marcador de revisão após a redescoberta. O Windows pode resolver um endereço privado em uma identidade estável. Um endereço constante na API não prova que o endereço transmitido pelo rádio permaneceu constante.
3. **Fluxo físico.** Em etapa aprovada à parte, avaliar a necessidade de assinatura dos CCCDs e de inicialização específica por revisão. Exigir evidência de frames F0 reais para K1–K10, clique e dois sentidos do roller, incluindo estados simultâneos e soltura. Registrar F2 e F8 separadamente. Os resultados Telink e Nordic devem ter matrizes de hardware próprias.
4. **Transporte futuro.** Implementar na camada Windows uma fábrica BLE compatível com `IAck05ReportSourceFactory`. Cada conexão terá fila limitada, cancelamento próprio e descarte de callbacks atrasados. Entregar ao `Ack05InputService` apenas relatórios físicos F0 validados para a revisão. Reaproveitar decodificador, tracker, perfis, gestos, combos, menus e a liberação P0. Um encerramento ou perda de dados precisa invalidar a sessão e descartar o estado retido. A escolha entre USB e BLE pertence ao host e deve evitar consumo simultâneo da mesma unidade. A interface continua iniciando em Output OFF.

## Verificação

Build Release dos seis projetos com zero avisos e zero erros. **56/56 SelfTests aprovados**, incluindo os 51 anteriores. Cinco casos BLE usam `IReadOnlyBleLink` simulado e cobrem seleção dinâmica e ambiguidade, operações permitidas por serviço, CCCD desligado, conflito de acesso, identidade divergente, assinatura incompleta, revisão desconhecida, classificação de frames e callbacks atrasados após cancelamento ou reconexão. Os testes físicos pendentes são a leitura de notificações entregues ao processo, captura sincronizada com controles físicos, rotação de endereço e validação independente em unidade Nordic. [Testes BLE](../../tests/Ack05Enhanced.SelfTest/BleDiagnosticTests.cs)
