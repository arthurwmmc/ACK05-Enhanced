# P0.8 · Ensaio único de ativação

**Resultado inconclusivo antes da escrita.** Em 24/11/2026, o probe redescobriu
um candidato por nome e confirmou o PnP ID 28BD:0202. A verificação de ausência
de XPPenTablet passou. O serviço FFE0 abriu em SharedReadAndWrite. A característica
0001 anunciou WriteWithoutResponse e Write. As leituras de ambos os CCCDs
retornaram Notify e os ouvintes foram registrados em ordem, 0002 seguido de 0003.

| Evento | Horário de São Paulo |
| --- | --- |
| PnP confirmado | 22:20:17.362 |
| FFE0 aberto | 22:20:17.844 |
| Dois ouvintes instalados | 22:20:18.299 |
| Encerramento | 22:20:56.746 |

O limite de preparação foi de 45 segundos, incluindo descoberta. A janela com
ambos os ouvintes durou aproximadamente 38,45 s. Nenhum callback entregou
pacotes em 0002 ou 0003. Não houve heartbeat F2, frame F0 ou evento F8.
O prazo expirou e o processo terminou com código 3 após remover os ouvintes e
liberar os objetos. O número de chamadas de escrita foi zero. Horário de envio,
resultado GATT de escrita, intervalo entre pacotes e captura pós-envio não se aplicam.

A fase de acionamento individual não iniciou. Nenhum dos dez botões, o clique
central ou os sentidos do dial foi identificado. Os heartbeats obtidos em P0.7
pertencem a outra sessão. A diferença entre as sessões não determina a causa do
silêncio observado. O ensaio não mediu a eficácia do comando proprietário.

Build Release dos seis projetos com zero avisos e zero erros. Os 59 testes da
suíte SelfTest passaram, incluindo os testes simulados existentes da captura BLE.
Registro local integral em `captures/private/p08-single-trial.log`.
