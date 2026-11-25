# P0.10 · Auditoria de viabilidade do HandtaK

Data da análise em 25/11/2026. Projeto de origem OmniSoundsLab/HandtaK. Revisão auditada `fc86d24abeb7f2db2687e738935ab4edd68af7b9`, commit de 01/08/2026. Clone independente em `output/handtak-audit/HandtaK`, com árvore limpa ao fim da leitura. A análise cobriu todos os arquivos versionados, incluindo o workflow de build. Foram feitas apenas clonagem, leitura de fontes, consulta de documentação e leitura de propriedades PnP. [Revisão fixada](https://github.com/OmniSoundsLab/HandtaK/tree/fc86d24abeb7f2db2687e738935ab4edd68af7b9)

## 1. Parecer

**A arquitetura permite supressão por instância da pilha de teclado e é uma candidata plausível para o ACK05 Bluetooth. A revisão atual exige correções e validação isolada antes da integração.** A correspondência persistente entre o índice HandtaK e Col03 está incompleta na API. Também faltam garantias necessárias para lidar com teclas pressionadas, perda de eventos, concorrência entre clientes e desconexões.

A primeira integração proposta fica restrita a Observe, com Output OFF e seleção de uma única coleção comprovada por identidade. Isso depende de uma etapa anterior de identidade e segurança do driver. O comportamento físico do HandtaK com o ACK05 permanece sem ensaio nesta auditoria.

| Questão | Resultado da auditoria |
|---|---|
| Pode interceptar e suprimir um teclado separadamente? | Sim, por contexto de filtro e slot da instância. |
| É compatível arquiteturalmente com HID Bluetooth? | Plausível, por atuar na interface de classe acima de kbdhid. Sem demonstração física nesta revisão. |
| Expõe a identidade persistente exigida pelo projeto? | A API retorna somente Hardware IDs e índice. Faltam Instance ID, Container ID e geração da conexão. |
| Recupera após fechamento do cliente? | Existe restauração para Off em EvtFileClose. A garantia depende do fechamento efetivo do objeto e não reconcilia teclas. |
| Recupera de aplicativo travado com handle aberto? | Ausente política de expiração ou watchdog. |
| Impede teclas presas? | Ausente acompanhamento de make/break físico, entregue ao Windows e injetado. |
| Está pronto para implantação Windows 11 com Secure Boot/HVCI? | Pacote assinado e validação de compatibilidade permanecem pendentes. |

## 2. Material examinado e dependências

| Arquivos | Conteúdo relevante |
|---|---|
| README.md, LICENSE, NOTICE | Modelo de instalação, capacidades declaradas, licença MIT e origens. |
| sys/handtak.c, 1.555 linhas | DriverEntry, anexação PnP, callbacks de classe, IOCTLs, slots e ring buffer. |
| sys/handtak.h | Contexto por instância, declarações KMDF e remanescentes do exemplo PS/2. |
| sys/handtak_public.h | ABI de controle versão 3.0, modos, máscaras e estruturas de pacotes. |
| dll/interception.c, 498 linhas | Shim user-mode compatível com parte da API Interception. |
| HandtaK.sln, sys/HandtaK.vcxproj | Solução do driver, configurações x64, toolset WDK e bibliotecas. |
| sys/HandtaK.inx, sys/handtak.rc | Template INF para pacote de assinatura e metadados do binário. |
| Directory.Build.props, packages.config | SDK/WDK NuGet fixados em 10.0.28000.2526. |
| .github/workflows/build.yml, .gitignore | Build manual, limitações de CI e exclusão de certificados/binários gerados. |

A solução contém o projeto do driver. O arquivo da DLL está fora dos projetos de build da solução. O driver usa WDF/KMDF, cabeçalhos de teclado/mouse e bibliotecas `wdmsec`, `ntstrsafe` e `rtlver`. A DLL usa Win32, DeviceIoControl e a biblioteca C. O README indica Visual Studio 2026 com C++ e WDK, e os arquivos de build também descrevem uso local do EWDK. A árvore examinada carece de testes automatizados para o driver. O workflow relata problemas com caminhos dos kits, possui acionamento manual e gera artefatos sem assinatura. A consulta à API pública de releases encontrou zero releases em 25/11/2026. Isso limita a avaliação de um pacote distribuível e de sua assinatura.

## 3. Arquitetura e posição na pilha

O mesmo driver KMDF atende as classes Keyboard e Mouse. EvtDeviceAdd consulta a classe do PDO, cria um filtro com WdfFdoInitSetFilter, armazena o Hardware ID e reserva um slot. O controle global fica em `\\.\HandtaK`. O fluxo de teclado esperado para esta unidade é o seguinte, sujeito à confirmação da pilha real em laboratório.

```text
ACK05 Bluetooth / HID-over-GATT
    → HIDCLASS / PDO da coleção Col03
    → kbdhid
    → HandtaK, interceptação da conexão de classe
    → kbdclass
    → entrada do Windows

HandtaK → IOCTL RECEIVE → cliente user-mode
cliente → IOCTL SET_FILTER → modo da instância
cliente → IOCTL SEND → callback superior de classe
```

O filtro substitui os ponteiros de CONNECT_DATA em IOCTL_INTERNAL_KEYBOARD_CONNECT e IOCTL_INTERNAL_MOUSE_CONNECT, preservando o callback superior. Trabalha com KEYBOARD_INPUT_DATA/MOUSE_INPUT_DATA já traduzidos pela pilha. Os pacotes entregam MakeCode, Flags e UnitId, sem o VKey calculado por RAWKEYBOARD. Esse desenho evita dependência do transporte BLE ou do protocolo FFE0. A coleção digitalizadora não é atendida por esses dois filtros de classe. [Conexão no código](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L1101)

O registro como upper class filter deve posicioná-lo abaixo de kbdclass para interceptar a conexão, conforme o exemplo kbfiltr. A posição relativa aos demais filtros precisa ser verificada. A Microsoft recomenda frameworks KMDF/UMDF e permite upper filters nas camadas kbdhid/mouhid e kbdclass/mouclass, evitando substituição dos drivers do sistema. O HandtaK usa KMDF e o mecanismo documentado de callback. Uma migração para UMDF exigiria outro desenho, pois este callback executa em DISPATCH_LEVEL no kernel. [Exemplo kbfiltr](https://learn.microsoft.com/en-us/samples/microsoft/windows-driver-samples/keyboard-input-wdf-filter-driver-kbfiltr/) · [Orientação HID](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/keyboard-and-mouse-hid-client-drivers) · [Callback de classe](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/kbdmou/nc-kbdmou-pservice_callback_routine)

## 4. Modos, API e DLL

| Modo | Teclado | Mouse |
|---|---|---|
| Off | Encaminha o lote ao callback superior. | Encaminha sem captura. |
| Observe | Copia o lote para o ring e o encaminha ao Windows. | Copia os pacotes que casam com a máscara e encaminha todos. |
| Suppress | Copia o lote e informa consumo integral sem encaminhá-lo. | Remove pacotes que casam com a máscara e encaminha os restantes. |

No teclado, a máscara não seleciona teclas nem tipos de evento. Suppress afeta todos os pacotes daquela instância. No mouse, a seleção opera por pacote. Movimento combinado com um botão ou roda selecionados também pode ser removido, pois o callback descarta o pacote inteiro. Essa condição limita a afirmação de que o movimento sempre mantém o caminho original. [Callbacks de captura](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L1327)

A interface tem cinco IOCTLs bufferizados, GET_VERSION, GET_DEVICE_INFO, SET_FILTER, RECEIVE e SEND. A ABI usa empacotamento de quatro bytes. Uma futura interoperabilidade C# deve verificar tamanhos de 1.040 bytes para HTK_DEVICE_INFO, 12 para HTK_SET_FILTER e 32 para HTK_STROKE. O campo HardwareIdLength contém bytes UTF-16 de um REG_MULTI_SZ, limitado a 512 WCHAR. A truncagem pode cortar uma string ou seu terminador e exige tratamento pelo comprimento. [Interface pública](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak_public.h)

**O shim interception.dll exige cuidado específico para Observe.** interception_set_filter mapeia qualquer filtro diferente de zero para Suppress. A API exportada desse arquivo carece de uma operação para selecionar Observe. Uma integração observacional teria de usar diretamente o IOCTL validado ou uma interface própria restrita a Observe. O shim também armazena precedência sem aplicá-la à fila e atualiza seu filtro local sem confirmar sucesso do SET_FILTER. [Mapeamento da DLL](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/dll/interception.c#L271)

## 5. ACK05 Bluetooth e identidade persistente

O P0.9 demonstrou que esta unidade entrega K1–K10 e os dois giros em Col03, uso 0001/0006. Em 25/11/2026, a consulta PnP somente leitura dessa mesma instância retornou serviço kbdhid e classe Keyboard `{4D36E96B-E325-11CE-BFC1-08002BE10318}`. Esse fato sustenta a hipótese de que o callback de classe do HandtaK alcançaria os mesmos scan codes.

O Container ID consultado corresponde ao registrado na sessão P0.9. Os Hardware IDs locais incluem os seguintes valores. O prefixo 02 em VID&0228bd integra a codificação do enumerador Bluetooth.

```text
HID\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&0228bd_PID&0202_REV&0001&Col03
HID\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&0228bd_PID&0202&Col03
HID\{00001812-0000-1000-8000-00805f9b34fb}_LOCALMFG&0046&Col03
HID\VID_28BD&UP:0001_U:0006
HID_DEVICE_SYSTEM_KEYBOARD
HID_DEVICE_UP:0001_U:0006
HID_DEVICE
```

A lista ocupa 622 bytes com os terminadores, cabendo no limite atual de 1.024 bytes. Ela permite selecionar candidatos com o modelo, transporte e coleção corretos. Esses IDs podem ser compartilhados por duas unidades ACK05 iguais. O Instance ID completo registrado no P0.9 também contém o trecho individual do enumerador e o sufixo da instância. O Hardware ID retornado pelo HandtaK omite esses componentes e o Container ID.

O código lê DevicePropertyHardwareID do PDO e associa o resultado ao primeiro slot livre, 1–10 para teclados e 11–20 para mouses. O índice pode mudar ou ser reutilizado após remoção. UnitId integra o pacote de classe e tampouco serve como identidade persistente. O hDevice de Raw Input pertence a outra API. [Registro de slots](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L272) · [Consulta do PDO](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L911)

Portanto, a API atual não comprova a ligação persistente entre um slot e a instância Col03 conhecida. Um único candidato por Hardware ID reduz ambiguidade naquele inventário, mas não cumpre o requisito de identidade persistente. A Microsoft distingue os Hardware IDs usados para correspondência de pacotes do Device Instance ID que identifica um devnode no sistema. [Hardware IDs](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/hardware-ids) · [Device Instance ID](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/device-instance-ids)

A extensão futura recomendada deve publicar Instance ID obtido do PDO correspondente, Container ID, classe, geração monotônica da anexação e tamanho/versão da resposta. O adaptador resolveria esses dados por SetupAPI, cruzaria com a coleção HID 0001/0006 e o contêiner aprovado e ativaria Observe somente para uma correspondência inequívoca. SET_FILTER e cada evento precisariam carregar a geração para impedir que um slot reciclado fosse aceito como a sessão anterior. Após reconexão ou alteração de identidade, repetir a resolução. A durabilidade do Container ID diante da rotação de endereço Bluetooth ainda exige ensaio.

## 6. Segurança e recuperação do aplicativo

### Achados comprovados pelo fluxo de código

| Prioridade | Evidência | Consequência |
|---|---|---|
| Alta | SDDL concede acesso total a Everyone e os IOCTLs usam FILE_ANY_ACCESS. | Um processo com acesso ao endpoint pode observar, alterar modos e solicitar injeção em outros dispositivos. A superfície inclui teclados convencionais. |
| Alta | SET_FILTER aceita outro proprietário e índice zero para todos os slots. SEND e RECEIVE carecem de checagem de proprietário. | Outro handle pode tomar ou desligar um filtro, consumir eventos ou injetar. O isolamento do campo Owner limita-se à rotina de fechamento. |
| Alta | Sem registro de teclas físicas, entregues ou injetadas. | Fechar o cliente ou trocar de modo não garante soltura de teclas que o Windows recebeu. |
| Alta | Ausência de watchdog e de expiração de sessão. | Um cliente travado com handle aberto mantém Suppress indefinidamente. |
| Alta | Ring e fila RECEIVE são globais e o evento contém somente o índice temporário. | Eventos podem ser consumidos por outro cliente ou associados a uma nova instância que reutilizou o índice. |
| Média | Ring de 512 eventos descarta o mais antigo e não expõe HtkDropped. | O consumidor não detecta perda de make/break nem consegue reconstruir o estado com garantia. |

As permissões são visíveis na criação do controle. SET_FILTER sobrescreve Owner sem validar o proprietário anterior. Essa constatação restringe a declaração do README de exclusividade do controle por handle. A rotina de fechamento respeita Owner, enquanto os IOCTLs permitem mudanças por outros handles. Um endpoint restrito a um serviço autorizado, com sessão exclusiva ou posse validada, reduziria essa exposição. [ACL e ring](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L84) · [IOCTLs](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L483)

EvtFileClose volta os slots daquele proprietário para Off e limpa o ring quando nenhum filtro permanece ativo. O registro de EvtFileCleanup está vazio. O framework chama Close depois que as referências e I/Os pendentes forem liberadas. Requisições RECEIVE enfileiradas podem ser canceladas pelo KMDF, mas a auditoria não estabelece um prazo máximo de restauração. Um handle duplicado ou retido por outro processo também prolonga a vida do objeto. Recuperação de processo terminado é plausível, porém não equivale a recuperação de travamento com handle vivo. [Fechamento no driver](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L430) · [Configuração dos callbacks](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L706) · [EvtFileClose](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdfdevice/nc-wdfdevice-evt_wdf_file_close) · [Cancelamento KMDF](https://learn.microsoft.com/en-us/windows-hardware/drivers/wdf/canceling-i-o-requests)

Dois cenários demonstram a falta de reconciliação pelo fluxo existente.

1. Ctrl make passa em Observe. O modo muda para Suppress antes do break. O driver consome a soltura e o Windows conserva o Ctrl pressionado.
2. O cliente injeta um make pelo SEND e encerra antes do break. EvtFileClose muda apenas o modo. A tecla injetada permanece sem soltura produzida pelo HandtaK.

Esses são cenários derivados da análise estática, sem execução no computador. O futuro protocolo de entrada/saída precisaria distinguir estado físico, estado confirmado no Windows e saídas sintéticas. Uma transição só em estado neutro pode ser um primeiro limite de uso, mas o produto ainda precisaria lidar com perdas e desconexões inesperadas. A lógica P0 do ACK05 cancela gestos da aplicação. Ela não corrige estados inconsistentes criados abaixo da aplicação por um filtro de classe.

## 7. Desconexão, concorrência e injeção

### Remoção Bluetooth e reutilização de sessão

HtkEvtDeviceCleanup limpa o slot, o proprietário e os ponteiros da conexão quando o objeto WDF é descartado. A fila global conserva eventos desse dispositivo se ainda existirem outros filtros ativos. O pacote carece de identificador de geração. Há, portanto, uma possibilidade concreta de leitura tardia do slot antigo após sua reutilização. RECEIVE também não envia um evento de desconexão. [Limpeza do dispositivo](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L326)

Um desligamento do rádio BLE pode manter o devnode instalado. Present significa que o slot ainda contém um WDFDEVICE e não garante ligação de rádio ativa. O comportamento exato de sono, perda de alcance e reconexão precisa ser medido. O código carece de callbacks próprios para suspensão, retomada e surprise removal que publiquem tais mudanças ao cliente. IOCTL_INTERNAL_KEYBOARD_DISCONNECT retorna STATUS_NOT_IMPLEMENTED. Esse remanescente do exemplo não comprova, isoladamente, uma falha de remoção PnP, que segue o ciclo WDF.

### Buffer e leitores concorrentes

O spin lock protege o conteúdo global, porém RECEIVE retira eventos para qualquer requisição disponível, sem direcionamento ao proprietário. Dois clientes competem pelo mesmo fluxo. As estruturas de evento carecem de sequência, timestamp de captura no kernel, contador de perdas e snapshot de teclas. O shim acrescenta uma fila de 1.024 eventos e lotes de até 64. Se sua fila lotar, o restante do lote é descartado sem sinalização de perda.

Há ainda uma janela entre a leitura do modo no callback e a captura/encaminhamento. Um SET_FILTER ou fechamento concorrente pode ocorrer nesse intervalo. O código não garante uma transição atômica entre o último evento do modo anterior e o primeiro do seguinte. Em Observe, uma nova tentativa de entrega de um lote parcialmente consumido pela classe superior também pode duplicar a cópia observada. Esses casos pedem testes de ordenação, transição e consumo parcial. [Retirada do ring](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L207) · [Fila da DLL](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/dll/interception.c#L184)

### Injeção e vida útil dos objetos

SEND entrega os pacotes diretamente ao callback superior salvo, em DISPATCH_LEVEL. Esse caminho passa acima do próprio callback de captura, reduzindo o risco de recaptura pelo mesmo filtro. Contudo, aceita solicitações para qualquer slot válido e confia nos campos fornecidos pelo cliente. HtkInjectStroke devolve sucesso após chamar a classe, sem verificar se consumed confirmou a entrada. O contador sent contabiliza esse sucesso lógico. O shim omite o buffer de saída com o contador e retorna zero em falhas parciais, mesmo se parte dos eventos já tiver sido enviada. [Injeção no driver](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/handtak.c#L355) · [SEND na DLL](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/dll/interception.c#L411)

O callback e ClassDeviceObject são copiados sob lock e usados depois da liberação. Nesse caminho não aparece referência de objeto ou proteção de rundown que acompanhe a chamada até seu término. Uma corrida com remoção merece revisão de vida útil e teste com Driver Verifier. Esse é um risco inferido da inspeção, sem demonstração de crash. O caminho de filtragem de mouse também compacta pacotes no buffer original, o que exige validar a contagem de consumo quando a classe superior aceita apenas parte deles.

## 8. Windows 11, assinatura e HVCI

A configuração Release/x64 usa KMDF, TargetVersion Windows10, DriverTargetPlatform Universal e WDK atual. Isso é coerente com execução em Windows 11 x64, mas os rótulos do projeto não certificam compatibilidade. ExAllocatePool2 aparece no código e requer Windows 10 2004 ou posterior, apesar de o template INF anunciar 16299 como mínimo. Windows 11 satisfaz esse piso, enquanto a compatibilidade com builds antigos declarada pelo INF precisa ajuste. [ExAllocatePool2](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/nf-wdm-exallocatepool2)

O README exige bibliotecas Spectre, mas o vcxproj desativa Driver_SpectreMitigation nas configurações. Também faltam evidências versionadas de execução de InfVerif, HLK e Driver Verifier. A declaração Universal e o uso de KMDF não demonstram que esses testes foram aprovados. O driver não foi compilado nem carregado nesta auditoria.

A distribuição em Windows 11 x64 com as políticas atuais exige um pacote/binário com assinatura aceita pela Microsoft e pelas políticas locais. Um certificado de teste local não constitui uma rota de implantação normal com Secure Boot. A documentação atual distingue assinatura por atestação para cenários de teste e validação WHCP/HLK para distribuição de produção. A assinatura por si só tampouco demonstra qualidade funcional. A revisão contém fontes e template INF, com ausência de release público para inspecionar a assinatura. [Assinatura de drivers](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/driver-signing) · [Opções de assinatura](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/driver-signing-offerings)

Memory Integrity/HVCI exige código e memória compatíveis com as restrições de integridade. A inspeção não encontrou geração dinâmica de código ou alocação explicitamente executável no caminho próprio de alocação. Isso é insuficiente para declarar compatibilidade. São necessários exame do binário final, testes de Code Integrity/HLK e execução com HVCI ativo em ambiente isolado. Secure Boot, HVCI e assinatura são critérios distintos de validação. [Compatibilidade HVCI](https://learn.microsoft.com/en-us/windows-hardware/drivers/driversecurity/implement-hvci-compatible-code)

## 9. Instalação, desinstalação e recuperação

**O repositório não fornece um instalador, desinstalador ou roteiro de recuperação completos.** README e INX descrevem um serviço kernel e UpperFilters de classe. O INX é um template de pacote para assinatura, com correspondência Root\HandtaK deliberadamente genérica e sem registro dos filtros de classe. Instalá-lo pelo fluxo PnP isoladamente não implementa o modelo de instalação descrito. A instalação real pertence a uma camada externa ao material auditado. [Template INX](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/sys/HandtaK.inx)

A implementação de um instalador futuro exigiria uma revisão própria. A sequência abaixo descreve os requisitos de um procedimento de laboratório, sem representar um procedimento já validado pelo projeto.

1. Registrar a versão do Windows, as políticas de integridade, o estado da pilha e a lista completa e ordenada de UpperFilters das classes afetadas. Preparar acesso de recuperação fora da pilha filtrada e acesso ao volume do Windows, inclusive chave BitLocker quando aplicável.
2. Validar hash, arquitetura, assinatura e dependências do pacote escolhido. Preparar arquivo e serviço em uma localização administrada antes de registrar o filtro. O StartType=3 e ErrorControl=0 do INX descrevem o template, não comprovam os valores de um instalador externo.
3. Registrar o filtro preservando kbdclass, mouclass e os demais filtros já existentes, com posição correta na pilha. Uma edição de REG_MULTI_SZ exige preservação dos outros elementos e da ordem necessária. O escopo original inclui todas as instâncias das duas classes, inclusive teclados e mouses convencionais.
4. Reiniciar o ambiente de teste e confirmar anexação, pass-through sem cliente e recuperação antes de abrir um consumidor. Iniciar o serviço isoladamente não comprova anexação às pilhas PnP já iniciadas.
5. Para desinstalar, parar o consumidor e reconciliar quaisquer saídas confirmadas. Remover somente o elemento HandtaK das listas em que foi adicionado. Reiniciar para desmontar as pilhas que o utilizavam e verificar teclado/mouse. Remover o serviço, binário e eventual pacote do Driver Store conforme o inventário real de instalação, após liberar as referências.

A Microsoft documenta a instalação de filtros por dispositivo e por classe. Para este caso de uso, vale avaliar um pacote restrito à coleção de teclado ACK05 em vez do alcance original das duas classes. A escolha e a posição do filtro exigem validação do pacote e da pilha. [Instalação de filtros](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/installing-a-filter-driver)

O pass-through de Off funciona quando o driver carregou e os callbacks estão íntegros. Uma falha de assinatura, arquivo ausente ou erro de anexação pode impedir o início dos dispositivos que referenciam o filtro. Assim, a proteção do aplicativo não cobre falha de carregamento do driver. Apagar o .sys enquanto UpperFilters ainda o referencia deixa a configuração inconsistente.

Uma recuperação pelo Windows RE é tecnicamente possível mediante edição offline da hive SYSTEM do volume correto. O procedimento precisa carregar a hive sob nome temporário, identificar os ControlSet efetivos pelos valores de Select e pelo backup, restaurar somente as entradas HandtaK afetadas nas classes Keyboard/Mouse e preservar os demais filtros. O alias CurrentControlSet da sessão de recuperação não deve ser usado como identificação da instalação offline. Depois, descarregar a hive e validar a inicialização. A remoção de registro de filtro precisa preceder a retirada de seu serviço/binário. O suporte Microsoft descreve essa técnica geral para filtros em recuperação de boot. A aplicação a estas duas classes deve ser ensaiada em uma cópia descartável. [Recuperação offline e filtros](https://learn.microsoft.com/en-us/windows/client-management/troubleshoot-inaccessible-boot-device)

O projeto também alerta para bloqueio por anti-cheat. Essa interação demanda avaliação dos ambientes em que o produto será utilizado. Não há base nesta auditoria para afirmar compatibilidade com essas políticas.

## 10. Integração recomendada e reutilização

Recomendo avaliar um fork pequeno e auditável do HandtaK antes de decidir por uma implementação kernel independente. O mecanismo de classe já existe, mas identidade, proteção da interface e ciclo de vida precisam endurecimento. Se o custo dessas mudanças comprometer a verificabilidade, um filtro mínimo derivado do kbfiltr oficial e restrito ao teclado ACK05 passa a ser a alternativa técnica. Nenhuma dessas opções está implementada nesta etapa.

```text
Filtro KMDF, identidade PnP e sessão versionada
    → serviço/cliente Windows com acesso restrito
    → adaptador de entrada ACK05, inicialmente Observe
    → estado físico validado
    → Core existente, camadas, combos e menus radiais
    → política de saída e executor existente
```

O código específico de HandtaK ficaria no lado Windows. O Core receberia estados de controles pela arquitetura comum. O adaptador reconstruiria make/break e modificadores por sessão e usaria o mapeamento validado no P0.9, preservando os limites de ambiguidade dos acordes HID. O modo Observe manteria os atalhos nativos e produziria apenas diagnóstico durante a primeira integração. Em produção, a escolha de saída e a autorização de supressão precisariam políticas próprias e estados explícitos. O Output OFF do ACK05 continua sendo uma política de execução, distinta de Off/Observe no driver.

Antes de integrar, os requisitos técnicos que decorrem dos achados são os seguintes.

- Publicar identidade persistente e geração, validar a seleção em cada mudança de modo e entregar eventos com sequência e sinalização de perda.
- Restringir a interface e validar posse de slots e requisições por sessão. Eliminar a necessidade de alterar todos os dispositivos para atender o ACK05.
- Definir as transições de modo, a expiração do cliente, o encerramento e a reconciliação de teclas confirmadas no Windows. Um timeout deve cancelar a captura com segurança e impedir confirmação de gestos pendentes.
- Sincronizar remoção com callbacks e injeções em andamento, descartar eventos antigos e publicar mudanças de disponibilidade.
- Preparar e testar o pacote de instalação, a remoção e a recuperação, com validação independente do binário assinado.

K11 e a bateria continuam sujeitos às capacidades observadas no caminho HID. Adotar o filtro não cria relatórios ausentes do periférico.

## 11. Licenciamento e proveniência

LICENSE concede MIT e inclui avisos de Omni Sounds Lab., Microsoft Corporation e Valentin-Gabriel Radu. NOTICE atribui a estrutura a kbfiltr e ao projeto valinet/xps15kbfix. Os códigos de controle, ring, modos e filtro de mouse são atribuídos pelo mantenedor ao HandtaK. Os nomes da API Interception são usados pelo shim, e a árvore examinada não inclui uma biblioteca externa Interception. Caso outro componente seja trazido posteriormente, sua licença precisa ser verificada separadamente.

Qualquer reutilização deve acompanhar LICENSE e NOTICE integrais e a revisão de origem. O clone conserva ambos. O relatório descreve o comportamento e não incorpora implementação ao ACK05. Os kits Microsoft e bibliotecas de build mantêm seus termos de distribuição próprios. [LICENSE](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/LICENSE) · [NOTICE](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/NOTICE) · [Dependências fixadas](https://github.com/OmniSoundsLab/HandtaK/blob/fc86d24abeb7f2db2687e738935ab4edd68af7b9/packages.config)

## 12. Plano de testes isolados

As fases abaixo são uma proposta sujeita à aprovação. Elas não foram executadas nesta auditoria. Os testes do ACK05 existentes não validam um driver que ainda não foi carregado.

| Fase | Ensaios | Critério de passagem |
|---|---|---|
| A · fontes e ABI | Build Release/x64 reproduzível com kits fixados, análise estática, InfVerif, tamanhos e limites de IOCTL, MULTI_SZ truncado e entradas inválidas. | Binário rastreável e ABI consistente. Ausência de alertas de segurança pendentes. |
| B · instalação e retorno | Windows 11 descartável, backup da configuração, instalar sem cliente, reiniciar, remover e recuperar offline. Driver Verifier direcionado ao filtro. | Teclado e mouse continuam utilizáveis, listas de filtros preservadas e recuperação demonstrada. |
| C · identidade | Observe em teclado de teste, dois dispositivos com VID/PID iguais, slots reciclados, ordenação diferente de chegada e múltiplas coleções. | Correspondência exata com Instance ID/Container ID e geração. Fonte ambígua rejeitada. |
| D · Bluetooth real | Máquina de laboratório com BLE, ACK05 e teclado de referência, K1–K10 e dial, sono, retomada, fora de alcance, rádio perdido e reconexão. | Mesmas sequências do P0.9 em Col03, pass-through integral, nenhuma atribuição cruzada. Passagem Bluetooth real necessária mesmo após teste em VM. |
| E · falhas e concorrência | Fechamento normal, término forçado, handle duplicado, processo travado, RECEIVE pendente, dois clientes e saturação dos buffers. | Encerramento limitado e documentado, perda detectável, ausência de apropriação e eventos antigos. |
| F · integridade do pacote | Binário final assinado, Secure Boot e HVCI ativos, testes HLK/Code Integrity e coexistência com filtros de terceiros em laboratório. | Evidência de carregamento, estabilidade e retorno às condições de entrada anteriores. |
| G · supressão futura | Somente após autorização específica, make antes/depois da troca de modo, perda de break, injeção parcial, crash e desconexão com teclas pressionadas. | Nenhuma tecla presa, nenhuma alteração nos demais teclados e nenhum efeito de sessão encerrada. |

A primeira integração, se aprovada após as fases anteriores pertinentes, deve usar Observe para uma única identidade resolvida e Output OFF. A fase G representa outra autorização e não integra a primeira validação observacional.

## 13. Limites da conclusão e entregáveis

O clone foi lido integralmente, sem execução de scripts de instalação, build do driver ou carregamento de binários externos. A consulta PnP apenas leu propriedades já registradas pelo Windows para a instância conhecida. P0, Core, USB e Raw Input não receberam alterações nesta etapa. A validação de 60/60 SelfTests pertence ao P0.9 e não foi repetida por esta auditoria documental.

Os entregáveis são este relatório e o clone fixado. A avaliação técnica permite continuar a análise de um fork restrito, mas ainda deixa pendentes identidade persistente na ABI, segurança do controle, recuperação de estado e validação do pacote. A decisão sobre implementação e ensaios de driver fica para a avaliação destes resultados.
