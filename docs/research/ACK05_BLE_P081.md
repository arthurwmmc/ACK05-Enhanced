# P0.8.1 · Reprodutibilidade da captura BLE

Em 24/11/2026, às 22:25:56 de São Paulo, o probe confirmou por leitura o PnP ID
28BD:0202 e iniciou o mesmo caminho de captura de P0.7. XPPenTablet estava
encerrado. A instrumentação adicional consultou a identidade e acompanhou a
conexão e os eventos de estado da GattSession de FFE0. A propriedade
MaintainConnection não foi alterada.

| Ensaio | Abertura de FFE0 | Tempo com ouvintes | CCCDs 0002/0003 | Pacotes 0002/0003 |
| --- | --- | --- | --- | --- |
| P0.7 | SharedReadOnly | 60 s | Notify / Notify | 0 / 11 |
| P0.8 | SharedReadAndWrite | 38,45 s | Notify / Notify | 0 / 0 |
| P0.8.1 | SharedReadOnly | 61,01 s | Notify / Notify | 0 / 0 |

O único hexadecimal observado na comparação é histórico, de P0.7:
`02 F2 01 50 00 00 00 00 00 00`. Nesta rodada não há payload nem intervalo
entre pacotes. A conexão estava Connected e a sessão Active antes e depois
da janela, sem eventos intermediários de mudança de estado, desconexão ou
exceção registrada. O processo encerrou normalmente e descartou os ouvintes,
sessão, serviços e dispositivo. O critério de aprovação não foi atingido.

## Comparação de código

P0.7 e P0.8.1 enumeram os serviços sem cache e mantêm referências aos serviços
e características em WindowsBleLink. BleNotificationCapture lê os dois CCCDs,
conecta delegates distintos a 0002 e 0003, nessa ordem, e mantém objetos de
registro em uma pilha até o finally. P0.8 consultou o serviço de identidade
separadamente, descartou-o e buscou FFE0 por UUID. Usou variáveis locais vivas
na operação assíncrona e o mesmo método de callback nos dois eventos, distinguindo
o emissor pelo UUID. Os dois registros antecederam a espera, e a remoção ficou
nos blocos finally. O callback registrava o pacote antes de filtrar F2. A
exceção de P0.8 foi o cancelamento pelo prazo de preparação. Os registros
confirmam que a instalação local retornou; não medem transmissão pelo rádio.

## Interpretação e decisão

SharedReadOnly e o caminho de delegates de P0.7 também produziram silêncio.
A hipótese de trabalho é uma diferença no estado de emissão do periférico ou
na entrega da assinatura entre sessões. Os ensaios não distinguem essas duas
explicações nem isolam efeitos de abertura do serviço. A Microsoft documenta
que escrever a configuração de notificação no CCCD, com ValueChanged registrado,
habilita a recepção. Ler Notify não demonstra que o aplicativo atual executou
essa negociação. Nenhuma escrita foi realizada no ensaio.
[Documentação Microsoft](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattcharacteristic.writeclientcharacteristicconfigurationdescriptorasync)

Após avaliação, o próximo controle sugerido é repetir somente leitura com
estado de vigília conhecido e registrar quando o operador despertou o controle.
Manter modo, duração e caminho de captura fixos. Uma captura passiva independente
poderia distinguir tráfego emitido de entrega ao processo, se disponível.
O handshake segue suspenso.

Build Release com zero avisos e zero erros. SelfTest 59/59.
Log privado: `captures/private/p081-20261124-222549.log`.
