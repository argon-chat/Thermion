# Thermion

**Thermion** is a lightweight controller for managing and scaling ICE servers (coturn) in distributed infrastructure.

It is built in C# and designed to run as a background service or sidecar, automating lifecycle operations for coturn instances in Docker Swarm environments.

---

## 🔧 Features

- **Docker Swarm integration**  
  Reads service metadata and scales coturn replicas via the local Docker socket.

- **Consul service registration**  
  Automatically registers each node running coturn in a Consul service catalog, with health checks.

- **Cloudflare DNS sync**  
  Dynamically creates or updates A/AAAA records in Cloudflare for each coturn instance.

- **Vault integration**  
  Pulls shared HMAC secret from HashiCorp Vault to configure TURN REST API authentication.

- **Single-node and global operation**  
  Can run locally on each node or globally with awareness of node state.

---
