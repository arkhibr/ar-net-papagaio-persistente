/**
 * Idempotency-Key de uma ação do usuário (arquitetura/06; contratos-front.md, seção 8;
 * adaptacao-bff-angular.md, F6). A mesma chave vale para os reenvios da mesma ação (duplo clique,
 * nova tentativa depois de erro de rede ou 409). Uma chave nova só nasce depois de sucesso
 * (concluir) ou quando o conteúdo muda: a API responde 422 se a chave antiga vier com outro corpo.
 */
export class ChaveDeIdempotencia {
  private chave = crypto.randomUUID();
  private conteudo: string | null = null;

  para(conteudo: unknown = null): string {
    const serializado = JSON.stringify(conteudo);
    if (this.conteudo !== null && this.conteudo !== serializado) {
      this.chave = crypto.randomUUID();
    }
    this.conteudo = serializado;
    return this.chave;
  }

  concluir(): void {
    this.chave = crypto.randomUUID();
    this.conteudo = null;
  }
}
